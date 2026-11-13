"""Builds and uploads the data behind the chemistry pages of wiki.ss14.pl.

    python Tools/_Polonium/wiki_chemistry.py build <chemistry.json> <out dir>
    python Tools/_Polonium/wiki_chemistry.py upload <out dir> [--token-file <path>]

The export comes from Content.WikiExporter (dotnet run --project Content.WikiExporter -- <dir> chemistry).
build stamps it with the date and commit, upload replaces it in the wiki asset folder.
The API token is read from WIKI_TOKEN unless --token-file is given. upload requires requests.
"""
import argparse
import json
import os
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
WIKI = "https://wiki.ss14.pl"
FOLDER = "chemia"
DATA_FILE = "dane.json"


def commit():
    sha = os.environ.get("GITHUB_SHA")
    if not sha:
        result = subprocess.run(["git", "-C", str(REPO), "rev-parse", "HEAD"], capture_output=True, text=True)
        sha = result.stdout.strip()
    return sha[:10]


def build(export_path, out_dir):
    data = json.loads(Path(export_path).read_text(encoding="utf-8-sig"))
    data["built"] = {"date": datetime.now(timezone.utc).strftime("%Y-%m-%d"), "commit": commit()}
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    (out / DATA_FILE).write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    print(f"{len(data['reagents'])} reagents, {len(data['reactions'])} reactions, "
          f"{(out / DATA_FILE).stat().st_size // 1024} KiB of data")


def upload(out_dir, token_file):
    import requests

    token = Path(token_file).read_text(encoding="utf-8").strip() if token_file else os.environ.get("WIKI_TOKEN", "")
    if not token:
        raise SystemExit("no API token: set WIKI_TOKEN or pass --token-file")
    headers = {"Authorization": "Bearer " + token}

    def gql(query, variables=None):
        response = requests.post(f"{WIKI}/graphql", json={"query": query, "variables": variables or {}},
                                 headers=headers, timeout=120)
        payload = response.json()
        if payload.get("errors"):
            raise SystemExit(json.dumps(payload["errors"], ensure_ascii=False)[:1000])
        return payload["data"]

    def folder_id():
        query = "{ assets { folders(parentFolderId: 0) { id slug } } }"
        found = next((f["id"] for f in gql(query)["assets"]["folders"] if f["slug"] == FOLDER), None)
        if found is None:
            gql("mutation ($slug: String!) { assets { createFolder(parentFolderId: 0, slug: $slug, name: $slug) "
                "{ responseResult { succeeded message } } } }", {"slug": FOLDER})
            found = next(f["id"] for f in gql(query)["assets"]["folders"] if f["slug"] == FOLDER)
        return found

    body = (Path(out_dir) / DATA_FILE).read_bytes()
    response = requests.post(f"{WIKI}/u", headers=headers, timeout=300, files=[
        ("mediaUpload", (None, json.dumps({"folderId": folder_id()}))),
        ("mediaUpload", (DATA_FILE, body, "application/json")),
    ])
    if response.status_code != 200:
        raise SystemExit(f"{DATA_FILE}: HTTP {response.status_code} {response.text[:300]}")
    check = requests.get(f"{WIKI}/{FOLDER}/{DATA_FILE}", headers={"Cache-Control": "no-cache"}, timeout=120)
    if check.content != body:
        raise SystemExit(f"{DATA_FILE}: the wiki serves different content after the upload")
    print(f"uploaded {WIKI}/{FOLDER}/{DATA_FILE} ({len(body) // 1024} KiB)")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    commands = parser.add_subparsers(dest="command", required=True)
    build_cmd = commands.add_parser("build")
    build_cmd.add_argument("export")
    build_cmd.add_argument("out")
    upload_cmd = commands.add_parser("upload")
    upload_cmd.add_argument("out")
    upload_cmd.add_argument("--token-file")
    args = parser.parse_args()
    if args.command == "build":
        build(args.export, args.out)
    else:
        upload(args.out, args.token_file)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
