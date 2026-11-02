"""Export the CLIP ViT-B/32 vision encoder (with projection) to ONNX.

Produces Models/clip-vit-b32.onnx with:
  input  : pixel_values  float32 [batch, 3, 224, 224]
  output : image_embeds  float32 [batch, 512]

Usage (from the repository root):
  python -m venv .venv
  .venv\\Scripts\\activate            # Windows  (source .venv/bin/activate on Linux/macOS)
  pip install torch transformers onnx
  python tools/export_clip_onnx.py
"""

from pathlib import Path

import torch
from transformers import CLIPVisionModelWithProjection

MODEL_ID = "openai/clip-vit-base-patch32"
OUTPUT = Path(__file__).resolve().parent.parent / "Models" / "clip-vit-b32.onnx"


def main() -> None:
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)

    model = CLIPVisionModelWithProjection.from_pretrained(MODEL_ID)
    model.eval()

    class VisionEmbeds(torch.nn.Module):
        def __init__(self, inner: torch.nn.Module) -> None:
            super().__init__()
            self.inner = inner

        def forward(self, pixel_values: torch.Tensor) -> torch.Tensor:
            return self.inner(pixel_values=pixel_values).image_embeds

    dummy = torch.randn(1, 3, 224, 224, dtype=torch.float32)

    torch.onnx.export(
        VisionEmbeds(model),
        (dummy,),
        str(OUTPUT),
        input_names=["pixel_values"],
        output_names=["image_embeds"],
        dynamic_axes={"pixel_values": {0: "batch"}, "image_embeds": {0: "batch"}},
        opset_version=17,
        dynamo=False,
    )

    print(f"Exported {MODEL_ID} vision encoder to {OUTPUT}")


if __name__ == "__main__":
    main()
