"""Regenerate the committed Windows icon and preview. Development only: Pillow required."""
from io import BytesIO
from pathlib import Path
import struct
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent
SIZES = (16, 20, 24, 32, 40, 48, 64, 128, 256)


def render(size):
    scale = size * 4 / 64
    image = Image.new("RGBA", (size * 4, size * 4))
    draw = ImageDraw.Draw(image)
    def box(coords):
        return tuple(round(value * scale) for value in coords)
    draw.rounded_rectangle(box((2, 2, 62, 62)), radius=round(13 * scale),
                           fill="#101b29", outline="#35594e", width=max(1, round(1.5 * scale)))
    vertices = ((16, 46), (16, 18), (23, 18), (32, 31), (41, 18), (48, 18),
                (48, 46), (40, 46), (40, 32), (32, 43), (24, 32), (24, 46))
    draw.polygon([box(vertex) for vertex in vertices], fill="#7fdbac")
    # Preserve the monogram's clarity at small taskbar/title-bar sizes.
    if size >= 32:
        for x in (24, 30, 36):
            draw.rounded_rectangle(box((x, 52, x + 4, 55)), radius=max(1, round(scale)), fill="#4b8e78")
    return image.resize((size, size), Image.Resampling.LANCZOS)


if __name__ == "__main__":
    frames = []
    for size in SIZES:
        stream = BytesIO()
        render(size).save(stream, format="PNG")
        frames.append(stream.getvalue())
    offset = 6 + 16 * len(SIZES)
    directory = bytearray(struct.pack("<HHH", 0, 1, len(SIZES)))
    for size, frame in zip(SIZES, frames):
        directory.extend(struct.pack("<BBBBHHII", size % 256, size % 256, 0, 0, 1, 32, len(frame), offset))
        offset += len(frame)
    (ROOT / "MatrixToolkit.ico").write_bytes(bytes(directory) + b"".join(frames))
    render(512).save(ROOT / "MatrixToolkit.png")
