from __future__ import annotations

import argparse
import os
import sys
from pathlib import Path
from typing import Iterable

import requests
from huggingface_hub import HfApi
from huggingface_hub.utils import get_token


CHUNK_SIZE = 1024 * 1024


def iter_selected_files(repo_files: Iterable[str], requested_files: list[str]) -> list[str]:
    if requested_files:
        selected = []
        available = set(repo_files)
        for requested in requested_files:
            if requested not in available:
                raise ValueError(f"Requested file is not present in repo: {requested}")
            selected.append(requested)
        return selected

    return list(repo_files)


def build_headers(token: str | None, range_start: int | None = None) -> dict[str, str]:
    headers: dict[str, str] = {}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    if range_start is not None and range_start > 0:
        headers["Range"] = f"bytes={range_start}-"
    return headers


def resolve_expected_size(response: requests.Response, resume_size: int) -> int | None:
    content_range = response.headers.get("Content-Range")
    if content_range and "/" in content_range:
        total = content_range.rsplit("/", 1)[-1].strip()
        if total.isdigit():
            return int(total)

    content_length = response.headers.get("Content-Length")
    if content_length and content_length.isdigit():
        return int(content_length) + (resume_size if response.status_code == 206 else 0)

    return None


def download_file(
    session: requests.Session,
    repo_id: str,
    revision: str,
    repo_file: str,
    destination_root: Path,
    token: str | None,
) -> None:
    url = f"https://huggingface.co/{repo_id}/resolve/{revision}/{repo_file}"
    target_path = destination_root / repo_file
    target_path.parent.mkdir(parents=True, exist_ok=True)
    temp_path = target_path.with_suffix(target_path.suffix + ".part")

    resume_size = temp_path.stat().st_size if temp_path.exists() else 0
    headers = build_headers(token, resume_size if resume_size > 0 else None)

    with session.get(url, headers=headers, stream=True, timeout=(20, 60)) as response:
        if response.status_code == 416:
            temp_path.rename(target_path)
            print(f"[skip] {repo_file} already complete")
            return

        response.raise_for_status()
        expected_size = resolve_expected_size(response, resume_size)
        mode = "ab" if response.status_code == 206 and resume_size > 0 else "wb"
        downloaded = resume_size if mode == "ab" else 0
        if mode == "wb" and temp_path.exists():
            temp_path.unlink()

        print(
            f"[download] {repo_file} status={response.status_code} "
            f"resume={resume_size} expected={expected_size if expected_size is not None else 'unknown'}",
            flush=True,
        )

        with temp_path.open(mode) as output_file:
            for chunk in response.iter_content(chunk_size=CHUNK_SIZE):
                if not chunk:
                    continue
                output_file.write(chunk)
                downloaded += len(chunk)
                if expected_size:
                    progress = downloaded / expected_size * 100
                    print(
                        f"[progress] {repo_file} {downloaded}/{expected_size} bytes ({progress:0.2f}%)",
                        flush=True,
                    )
                else:
                    print(f"[progress] {repo_file} {downloaded} bytes", flush=True)

    temp_path.replace(target_path)
    print(f"[done] {repo_file} -> {target_path}", flush=True)


def main() -> int:
    parser = argparse.ArgumentParser(description="Download Hugging Face repo files via raw HTTPS with resume support.")
    parser.add_argument("--repo-id", required=True)
    parser.add_argument("--revision", default="main")
    parser.add_argument("--local-dir", required=True)
    parser.add_argument("--file", action="append", dest="files", default=[])
    args = parser.parse_args()

    token = os.environ.get("HF_TOKEN") or get_token()
    session = requests.Session()
    session.trust_env = True

    api = HfApi(token=token)
    info = api.model_info(args.repo_id, revision=args.revision, files_metadata=True)
    repo_files = [sibling.rfilename for sibling in info.siblings]
    selected_files = iter_selected_files(repo_files, args.files)

    destination_root = Path(args.local_dir)
    destination_root.mkdir(parents=True, exist_ok=True)

    for repo_file in selected_files:
        download_file(session, args.repo_id, args.revision, repo_file, destination_root, token)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
