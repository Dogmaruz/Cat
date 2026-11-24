"""Deterministic seamless water-normal DATA texture; Fourier slopes, not painted colour."""
from pathlib import Path
import math
import numpy as np
from PIL import Image

size = 256
y, x = np.mgrid[:size, :size] / size
rng = np.random.default_rng(20261101)
height = np.zeros_like(x)
dx = np.zeros_like(x)
dy = np.zeros_like(x)
for _ in range(84):
    kx, ky = rng.integers(-22, 23, 2)
    frequency = math.hypot(kx, ky)
    if frequency < 2:
        continue
    amplitude = frequency ** -1.8
    phase = math.tau * (kx * x + ky * y) + rng.uniform(0, math.tau)
    height += np.sin(phase) * amplitude
    dx += np.cos(phase) * amplitude * math.tau * kx
    dy += np.cos(phase) * amplitude * math.tau * ky
scale = .42 / np.sqrt(np.mean(dx * dx + dy * dy))
normal = np.stack((-dx * scale, -dy * scale, np.ones_like(x)), axis=-1)
normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
rgba = np.empty((size, size, 4), dtype=np.uint8)
rgba[:, :, :3] = np.clip((normal * .5 + .5) * 255, 0, 255).astype(np.uint8)
rgba[:, :, 3] = np.clip((height / height.std() * .16 + .5) * 255, 0, 255).astype(np.uint8)
target = Path('DancingCatsClone/Assets/RadioArchipelago/Art/WaterRippleNormal.png')
target.parent.mkdir(parents=True, exist_ok=True)
Image.fromarray(rgba, 'RGBA').save(target)
print(target.resolve())
