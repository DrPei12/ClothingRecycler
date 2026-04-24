from __future__ import annotations

import argparse
import os
import sys
from pathlib import Path

from huggingface_hub import HfApi
from huggingface_hub.utils import get_token

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from hf_repo_fetch import download_file  # noqa: E402


DEFAULT_MODEL_ID = "fixie-ai/ultravox-v0_5-llama-3_2-1b"

COMPONENT_MANIFEST = {
    "adapter": {
        "repo_id": "fixie-ai/ultravox-v0_5-llama-3_2-1b",
        "files": [
            "config.json",
            "generation_config.json",
            "model.safetensors",
            "preprocessor_config.json",
            "processor_config.json",
            "special_tokens_map.json",
            "tokenizer.json",
            "tokenizer_config.json",
            "ultravox_config.py",
            "ultravox_model.py",
            "ultravox_pipeline.py",
            "ultravox_processing.py",
        ],
    },
    "text": {
        "repo_id": "meta-llama/Llama-3.2-1B-Instruct",
        "files": [
            "config.json",
            "generation_config.json",
            "model.safetensors",
            "special_tokens_map.json",
            "tokenizer.json",
            "tokenizer_config.json",
        ],
    },
    "audio": {
        "repo_id": "openai/whisper-large-v3-turbo",
        "files": [
            "added_tokens.json",
            "config.json",
            "generation_config.json",
            "merges.txt",
            "model.safetensors",
            "normalizer.json",
            "preprocessor_config.json",
            "special_tokens_map.json",
            "tokenizer.json",
            "tokenizer_config.json",
            "vocab.json",
        ],
    },
}


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


def default_bundle_root(model_id: str) -> Path:
    workspace_model_root = find_workspace_model_root()
    if workspace_model_root is not None:
        return workspace_model_root / model_id.rsplit("/", 1)[-1]

    local_app_data = Path.home() / "AppData" / "Local"
    if "LOCALAPPDATA" in os.environ:
        local_app_data = Path(os.environ["LOCALAPPDATA"])
    return local_app_data / "ClothingRecycler" / "AiModels" / model_id.rsplit("/", 1)[-1]


def validate_manifest(api: HfApi) -> None:
    for component_name, component in COMPONENT_MANIFEST.items():
        info = api.model_info(component["repo_id"], files_metadata=True)
        available = {sibling.rfilename for sibling in info.siblings}
        missing = [repo_file for repo_file in component["files"] if repo_file not in available]
        if missing:
            raise ValueError(f"{component_name} manifest references files missing from repo: {missing}")


def main() -> int:
    parser = argparse.ArgumentParser(description="Prepare a local Ultravox bundle for the desktop app.")
    parser.add_argument("--model-id", default=DEFAULT_MODEL_ID)
    parser.add_argument("--bundle-root")
    parser.add_argument(
        "--component",
        action="append",
        choices=sorted(COMPONENT_MANIFEST.keys()),
        dest="components",
        help="Limit downloads to specific component(s). Default: adapter + text + audio",
    )
    parser.add_argument(
        "--skip-weight-files",
        action="store_true",
        help="Download configs/tokenizers/code first and skip *.safetensors weights for this run.",
    )
    args = parser.parse_args()

    token = get_token()
    api = HfApi(token=token)
    validate_manifest(api)

    selected_components = args.components or ["adapter", "text", "audio"]
    bundle_root = Path(args.bundle_root) if args.bundle_root else default_bundle_root(args.model_id)
    bundle_root.mkdir(parents=True, exist_ok=True)

    session = __import__("requests").Session()
    session.trust_env = True

    for component_name in selected_components:
        component = COMPONENT_MANIFEST[component_name]
        destination_root = bundle_root / component_name
        destination_root.mkdir(parents=True, exist_ok=True)
        print(f"[component] {component_name} -> {destination_root}", flush=True)
        repo_files = component["files"]
        if args.skip_weight_files:
            repo_files = [repo_file for repo_file in repo_files if not repo_file.endswith(".safetensors")]

        for repo_file in repo_files:
            download_file(
                session=session,
                repo_id=component["repo_id"],
                revision="main",
                repo_file=repo_file,
                destination_root=destination_root,
                token=token,
            )

    print(f"[bundle-ready] {bundle_root}", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
