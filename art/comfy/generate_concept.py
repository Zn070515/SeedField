"""Send a Flux.1-dev concept prompt to a local ComfyUI API and copy the result.

Example:
  python art/comfy/generate_concept.py \
    --prompt-file art/prompts/power_node_concept.txt --seed 101 \
    --width 1024 --height 1024 --name power_node_concept_101

Requires ComfyUI at http://127.0.0.1:8188 with Flux.1-dev + T5/CLIP/VAE in
E:\\AI_ComfyUI\\Models. This is a concept-reference tool only; final game sprites
come from the Blender pipeline.
"""

from __future__ import annotations

import argparse
import json
import shutil
import time
import urllib.request
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
COMFY_OUTPUT = Path("E:/AI_ComfyUI/Output")
RENDERS = ROOT / "art/renders"
WORKFLOW_DIR = ROOT / "art/comfy"


def workflow(prompt: str, negative: str, seed: int, width: int, height: int) -> dict:
    return {
        "1": {
            "class_type": "UNETLoader",
            "inputs": {
                "unet_name": "flux1-dev.safetensors",
                "weight_dtype": "fp8_e4m3fn",
            },
        },
        "2": {
            "class_type": "DualCLIPLoader",
            "inputs": {
                "clip_name1": "clip_l.safetensors",
                "clip_name2": "t5xxl_fp8_e4m3fn.safetensors",
                "type": "flux",
            },
        },
        "3": {
            "class_type": "VAELoader",
            "inputs": {"vae_name": "ae.safetensors"},
        },
        "4": {
            "class_type": "CLIPTextEncode",
            "inputs": {"text": prompt, "clip": ["2", 0]},
        },
        "5": {
            "class_type": "CLIPTextEncode",
            "inputs": {"text": negative, "clip": ["2", 0]},
        },
        "6": {
            "class_type": "EmptySD3LatentImage",
            "inputs": {"width": width, "height": height, "batch_size": 1},
        },
        "10": {
            "class_type": "ModelSamplingFlux",
            "inputs": {
                "model": ["1", 0],
                "max_shift": 1.15,
                "base_shift": 0.5,
                "width": width,
                "height": height,
            },
        },
        "7": {
            "class_type": "KSampler",
            "inputs": {
                "model": ["10", 0],
                "seed": seed,
                "steps": 24,
                "cfg": 1.0,
                "sampler_name": "euler",
                "scheduler": "simple",
                "positive": ["4", 0],
                "negative": ["5", 0],
                "latent_image": ["6", 0],
                "denoise": 1.0,
            },
        },
        "8": {
            "class_type": "VAEDecode",
            "inputs": {"samples": ["7", 0], "vae": ["3", 0]},
        },
        "9": {
            "class_type": "SaveImage",
            "inputs": {
                "images": ["8", 0],
                "filename_prefix": "seedfield_concept",
            },
        },
    }


def post_json(url: str, payload: dict) -> dict:
    request = urllib.request.Request(
        url,
        data=json.dumps(payload).encode("utf-8"),
        headers={"Content-Type": "application/json"},
    )
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.loads(response.read().decode("utf-8"))


def get_json(url: str):
    with urllib.request.urlopen(url, timeout=30) as response:
        return json.loads(response.read().decode("utf-8"))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--prompt-file", required=True)
    parser.add_argument("--seed", type=int, default=101)
    parser.add_argument("--width", type=int, default=1024)
    parser.add_argument("--height", type=int, default=1024)
    parser.add_argument("--name", required=True)
    parser.add_argument("--server", default="http://127.0.0.1:8188")
    parser.add_argument("--timeout", type=int, default=900)
    args = parser.parse_args()

    prompt = Path(args.prompt_file).read_text(encoding="utf-8").strip()
    if not prompt:
        print("EMPTY_PROMPT")
        return 1

    graph = workflow(
        prompt,
        "text, watermark, logo, blurry, low contrast, cluttered, photorealistic",
        args.seed,
        args.width,
        args.height,
    )
    WORKFLOW_DIR.mkdir(parents=True, exist_ok=True)
    graph_path = WORKFLOW_DIR / f"workflow_{args.name}.json"
    graph_path.write_text(json.dumps(graph, indent=2), encoding="utf-8")

    client_id = uuid.uuid4().hex
    try:
        queued = post_json(
            f"{args.server}/prompt",
            {"prompt": graph, "client_id": client_id},
        )
    except Exception as error:
        print(f"COMFY_SUBMIT_FAILED {error}")
        return 1

    prompt_id = queued.get("prompt_id")
    if not prompt_id:
        print(f"COMFY_NO_PROMPT_ID {queued}")
        return 1
    print(f"COMFY_QUEUED {prompt_id}")

    deadline = time.time() + args.timeout
    history = None
    while time.time() < deadline:
        try:
            history = get_json(f"{args.server}/history/{prompt_id}")
        except Exception as error:
            print(f"COMFY_POLL_WARNING {error}")
        if history is not None and prompt_id in history:
            break
        time.sleep(2)

    if not history or prompt_id not in history:
        print("COMFY_TIMEOUT")
        return 1

    outputs = history[prompt_id].get("outputs", {})
    images = []
    for node in outputs.values():
        images.extend(node.get("images", []))
    if not images:
        print(f"COMFY_NO_IMAGE {history}")
        return 1

    RENDERS.mkdir(parents=True, exist_ok=True)
    copied = []
    for index, image in enumerate(images):
        source = COMFY_OUTPUT / image["subfolder"] / image["filename"]
        target = RENDERS / f"{args.name}_{index}.png"
        shutil.copy2(source, target)
        copied.append(str(target))

    print("COMFY_IMAGES " + " ".join(copied))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
