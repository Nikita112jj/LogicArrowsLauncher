from pathlib import Path
from PIL import Image

source = Path('/home/ubuntu/LogicArrowsLauncher/assets/logic-arrows-favicon.png')
target = Path('/home/ubuntu/LogicArrowsLauncher/assets/logic-arrows.ico')

image = Image.open(source).convert('RGBA')
if image.width != image.height:
    raise ValueError(f'Expected square favicon, got {image.size}')

image.save(
    target,
    format='ICO',
    sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)],
)
print(f'created={target}')
print(f'source={image.size}, mode={image.mode}')
with Image.open(target) as icon:
    print(f'ico_default={icon.size}, mode={icon.mode}')
