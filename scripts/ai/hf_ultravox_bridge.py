from __future__ import annotations

import argparse
import json
import os
import sys
import traceback
import wave
from pathlib import Path

import numpy as np

os.environ.setdefault("TRANSFORMERS_NO_TF", "1")
os.environ.setdefault("TRANSFORMERS_NO_FLAX", "1")
os.environ.setdefault("USE_TF", "0")
os.environ.setdefault("USE_FLAX", "0")
os.environ.setdefault("USE_TORCH", "1")
os.environ.setdefault("HF_HUB_DISABLE_XET", "1")


DEFAULT_MODEL_ID = "fixie-ai/ultravox-v0_5-llama-3_2-1b"
LOCAL_BUNDLE_ROOT_ENV = "CLOTHING_RECYCLER_HF_LOCAL_BUNDLE_ROOT"
SERVER_PREFIX = "__CR_JSON__:"
PIPELINE_CACHE: dict[str, object] = {}


def find_workspace_model_root() -> Path | None:
    candidates = [
        Path.cwd(),
        Path(__file__).resolve().parent,
    ]

    for candidate in candidates:
        for directory in [candidate, *candidate.parents]:
            if (directory / "ClothingRecycler.Desktop.csproj").exists():
                return directory / "artifacts" / "ai-models"

    return None


def emit(payload: dict) -> int:
    print(json.dumps(payload, ensure_ascii=False))
    return 0 if payload.get("ok") else 1


def emit_server(payload: dict) -> None:
    print(f"{SERVER_PREFIX}{json.dumps(payload, ensure_ascii=False, separators=(',', ':'))}", flush=True)


def read_request_payload(request_file: Path) -> dict:
    # Accept UTF-8 BOM so ad-hoc Windows-authored request files do not fail parsing.
    return json.loads(request_file.read_text(encoding="utf-8-sig"))


def resolve_bundle_root(model_id: str) -> Path:
    configured_root = os.environ.get(LOCAL_BUNDLE_ROOT_ENV)
    if configured_root:
        return Path(configured_root).expanduser().resolve() / model_id.rsplit("/", 1)[-1]

    workspace_model_root = find_workspace_model_root()
    if workspace_model_root is not None:
        return workspace_model_root.resolve() / model_id.rsplit("/", 1)[-1]

    local_app_data = os.environ.get("LOCALAPPDATA")
    if not local_app_data:
        local_app_data = str(Path.home() / "AppData" / "Local")

    return Path(local_app_data).resolve() / "ClothingRecycler" / "AiModels" / model_id.rsplit("/", 1)[-1]


def try_prepare_local_bundle(model_id: str) -> tuple[Path, Path, Path] | None:
    bundle_root = resolve_bundle_root(model_id)
    adapter_dir = bundle_root / "adapter"
    text_dir = bundle_root / "text"
    audio_dir = bundle_root / "audio"

    required_files = [
        adapter_dir / "config.json",
        adapter_dir / "model.safetensors",
        text_dir / "config.json",
        text_dir / "model.safetensors",
        audio_dir / "config.json",
        audio_dir / "model.safetensors",
    ]

    for required_file in required_files:
        if not required_file.exists():
            return None

    config_path = adapter_dir / "config.json"
    config = json.loads(config_path.read_text(encoding="utf-8"))
    changed = False

    text_path = str(text_dir)
    if config.get("text_model_id") != text_path:
        config["text_model_id"] = text_path
        changed = True

    # Ultravox remote code chooses a Whisper-specific load path by checking whether
    # "whisper" appears in the configured audio model id. Local "...\audio" paths
    # lose that hint, so keep a stable local path that still contains "whisper".
    whisper_hint_dir = bundle_root / "whisper-local-hint"
    whisper_hint_dir.mkdir(parents=True, exist_ok=True)
    audio_loader_path = str(whisper_hint_dir / ".." / "audio")
    if config.get("audio_model_id") != audio_loader_path:
        config["audio_model_id"] = audio_loader_path
        changed = True

    audio_config = config.setdefault("audio_config", {})
    if audio_config.get("_name_or_path") != audio_loader_path:
        audio_config["_name_or_path"] = audio_loader_path
        changed = True

    if changed:
        config_path.write_text(json.dumps(config, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    return bundle_root, adapter_dir, text_dir


def load_pcm_wave(path: Path) -> tuple[np.ndarray, int]:
    with wave.open(str(path), "rb") as wav_file:
        channels = wav_file.getnchannels()
        sample_width = wav_file.getsampwidth()
        sample_rate = wav_file.getframerate()
        frame_count = wav_file.getnframes()
        frames = wav_file.readframes(frame_count)

    if sample_width != 2:
        raise ValueError("Only 16-bit PCM .wav files are supported by this bridge.")

    audio = np.frombuffer(frames, dtype=np.int16).astype(np.float32) / 32768.0
    if channels > 1:
        audio = audio.reshape(-1, channels).mean(axis=1)

    return audio, sample_rate


def normalize_turns(raw_turns: list) -> list[dict[str, str]]:
    normalized: list[dict[str, str]] = []
    for item in raw_turns:
        if not isinstance(item, dict):
            continue

        role = item.get("role") or item.get("Role") or item.get("ROLE")
        content = item.get("content") or item.get("Content") or item.get("CONTENT")
        if role is None or content is None:
            continue

        normalized.append(
            {
                "role": str(role),
                "content": str(content),
            }
        )

    return normalized


def build_probe_payload(model_id: str) -> dict:
    local_bundle = try_prepare_local_bundle(model_id)
    if local_bundle is not None:
        bundle_root, adapter_dir, text_dir = local_bundle
        config_path = adapter_dir / "config.json"
        config = json.loads(config_path.read_text(encoding="utf-8"))
        audio_model_id = config.get("audio_model_id") or config.get("audio_config", {}).get("_name_or_path", "")
        return {
            "ok": True,
            "summary": f"Local Ultravox bundle is ready at {bundle_root}.",
            "model_id": model_id,
            "resolved_model_source": str(adapter_dir),
            "using_local_bundle": True,
            "text_model_id": str(text_dir),
            "audio_model_id": audio_model_id,
            "python": sys.version,
            "notes": [
                "This bridge will prefer the local Ultravox bundle over remote repo loading.",
                "Keep adapter/text/audio subdirectories complete before running real inference.",
            ],
        }

    try:
        from huggingface_hub import HfApi, hf_hub_download
    except Exception as exc:  # pragma: no cover - runtime env probe
        return {
            "ok": False,
            "summary": "huggingface_hub is not available.",
            "error": str(exc),
        }

    try:
        api = HfApi()
        adapter_info = api.model_info(model_id, files_metadata=True)
        config_path = hf_hub_download(model_id, "config.json")
        config = json.loads(Path(config_path).read_text(encoding="utf-8"))

        text_model_id = config.get("text_model_id", "")
        audio_model_id = config.get("audio_config", {}).get("_name_or_path", "")
        text_model_info = api.model_info(text_model_id, files_metadata=True) if text_model_id else None
        audio_model_info = api.model_info(audio_model_id, files_metadata=True) if audio_model_id else None

        adapter_size = sum((getattr(sibling, "size", 0) or 0) for sibling in adapter_info.siblings)
        text_model_size = sum((getattr(sibling, "size", 0) or 0) for sibling in (text_model_info.siblings if text_model_info else []))
        audio_model_size = sum((getattr(sibling, "size", 0) or 0) for sibling in (audio_model_info.siblings if audio_model_info else []))

        adapter_summary = f"Ultravox probe succeeded for {model_id}."
        if getattr(text_model_info, "gated", None):
            adapter_summary += " The text backbone still requires gated access."

        return {
            "ok": True,
            "summary": adapter_summary,
            "model_id": model_id,
            "adapter_total_size_bytes": adapter_size,
            "text_model_id": text_model_id,
            "text_model_gated": getattr(text_model_info, "gated", None),
            "text_model_total_size_bytes": text_model_size,
            "audio_model_id": audio_model_id,
            "audio_model_total_size_bytes": audio_model_size,
            "python": sys.version,
            "notes": [
                "Ultravox is an audio-text-to-text model built on a Whisper encoder plus a large text backbone.",
                "This repository contains the Ultravox adapter and still references a separate text backbone.",
                "A full local run on PC is possible only after the referenced text model and Whisper assets are available locally.",
            ],
        }
    except Exception as exc:  # pragma: no cover - runtime env probe
        return {
            "ok": False,
            "summary": "Ultravox probe failed.",
            "error": str(exc),
            "traceback": traceback.format_exc(),
        }


def build_pipeline(model_id: str):
    import transformers
    import torch

    model_kwargs: dict = {"low_cpu_mem_usage": True}
    local_bundle = try_prepare_local_bundle(model_id)
    resolved_model = str(local_bundle[1]) if local_bundle is not None else model_id
    pipeline_kwargs: dict = {
        "model": resolved_model,
        "trust_remote_code": True,
        "model_kwargs": model_kwargs,
    }

    if torch.cuda.is_available():
        model_kwargs["torch_dtype"] = torch.float16
        pipeline_kwargs["device_map"] = "auto"

    if local_bundle is not None:
        pipeline_kwargs["local_files_only"] = True

    return transformers.pipeline(**pipeline_kwargs)


def get_pipeline(model_id: str):
    pipeline = PIPELINE_CACHE.get(model_id)
    if pipeline is None:
        pipeline = build_pipeline(model_id)
        PIPELINE_CACHE[model_id] = pipeline
    return pipeline


def extract_generated_text(result) -> str:
    if isinstance(result, list) and result:
        return extract_generated_text(result[0])

    if isinstance(result, dict):
        text = (
            result.get("generated_text")
            or result.get("text")
            or result.get("output_text")
            or result.get("transcript")
        )
        if text is not None:
            return str(text)

        return json.dumps(result, ensure_ascii=False)

    return str(result)


def build_completion_payload(payload: dict, model_id_override: str | None = None, use_cache: bool = False) -> dict:
    try:
        model_id = payload.get("model_id") or model_id_override or DEFAULT_MODEL_ID
        turns = normalize_turns(payload.get("messages", []))
        max_new_tokens = int(payload.get("max_new_tokens", 512))
        audio_path = payload.get("audio_path")

        pipe = get_pipeline(model_id) if use_cache else build_pipeline(model_id)
        inference_payload: dict = {"turns": turns}

        if audio_path:
            audio, sampling_rate = load_pcm_wave(Path(audio_path))
            inference_payload["audio"] = audio
            inference_payload["sampling_rate"] = sampling_rate

        result = pipe(inference_payload, max_new_tokens=max_new_tokens)
        text = extract_generated_text(result)

        return {
            "ok": True,
            "text": text,
        }
    except Exception as exc:  # pragma: no cover - runtime env probe
        return {
            "ok": False,
            "error": str(exc),
            "traceback": traceback.format_exc(),
        }


def run_stdio_server(model_id: str) -> int:
    emit_server(
        {
            "ok": True,
            "summary": "Ultravox bridge server is ready.",
            "model_id": model_id,
        }
    )

    for raw_line in sys.stdin:
        line = raw_line.strip()
        if not line:
            continue

        try:
            message = json.loads(line)
            op = message.get("op")
            requested_model_id = message.get("model_id") or model_id

            if op == "probe":
                payload = build_probe_payload(requested_model_id)
            elif op == "warm":
                get_pipeline(requested_model_id)
                payload = {
                    "ok": True,
                    "summary": f"Ultravox model is warmed for {requested_model_id}.",
                }
            elif op == "complete":
                payload = build_completion_payload(message.get("request") or {}, requested_model_id, use_cache=True)
            elif op == "shutdown":
                emit_server(
                    {
                        "ok": True,
                        "summary": "Ultravox bridge server is shutting down.",
                    }
                )
                return 0
            else:
                payload = {
                    "ok": False,
                    "error": f"Unsupported operation: {op}",
                }
        except Exception as exc:
            payload = {
                "ok": False,
                "error": str(exc),
                "traceback": traceback.format_exc(),
            }

        emit_server(payload)


def run_probe(model_id: str) -> int:
    return emit(build_probe_payload(model_id))


def run_request(request_file: Path, model_id_override: str | None = None) -> int:
    payload = read_request_payload(request_file)
    return emit(build_completion_payload(payload, model_id_override, use_cache=False))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--probe", action="store_true")
    parser.add_argument("--stdio-server", action="store_true")
    parser.add_argument("--request-file", type=Path)
    parser.add_argument("--model-id", default=DEFAULT_MODEL_ID)
    args = parser.parse_args()

    if args.stdio_server:
        return run_stdio_server(args.model_id)

    if args.probe:
        return run_probe(args.model_id)

    if args.request_file is None:
        return emit(
            {
                "ok": False,
                "error": "Either --probe or --request-file must be provided.",
            }
        )

    return run_request(args.request_file, args.model_id)


if __name__ == "__main__":
    raise SystemExit(main())
