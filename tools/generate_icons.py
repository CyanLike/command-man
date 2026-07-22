from pathlib import Path

from PIL import Image, ImageDraw


OUTPUT = (
    Path(__file__).resolve().parents[1]
    / "src"
    / "Community.PowerToys.Run.Plugin.CommandMan"
    / "Images"
)
SCALE = 4


def create_icon(theme: str, panel: str, prompt: str, accent: str) -> None:
    image = Image.new("RGBA", (96 * SCALE, 96 * SCALE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle(
        (8 * SCALE, 12 * SCALE, 88 * SCALE, 84 * SCALE),
        radius=14 * SCALE,
        fill=panel,
    )
    draw.line(
        [
            (25 * SCALE, 33 * SCALE),
            (40 * SCALE, 48 * SCALE),
            (25 * SCALE, 63 * SCALE),
        ],
        fill=prompt,
        width=8 * SCALE,
        joint="curve",
    )
    draw.line(
        [(50 * SCALE, 64 * SCALE), (72 * SCALE, 64 * SCALE)],
        fill=accent,
        width=8 * SCALE,
    )
    draw.ellipse(
        (65 * SCALE, 19 * SCALE, 81 * SCALE, 35 * SCALE),
        fill=accent,
    )

    OUTPUT.mkdir(parents=True, exist_ok=True)
    image.resize((96, 96), Image.Resampling.LANCZOS).save(
        OUTPUT / f"commandman.{theme}.png",
        optimize=True,
    )


if __name__ == "__main__":
    create_icon("light", "#202020", "#ffffff", "#69d3ff")
    create_icon("dark", "#f4f4f4", "#171717", "#0078d4")
