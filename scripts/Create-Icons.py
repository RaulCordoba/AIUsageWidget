"""Generate the widget's existing orange/star motif without platform-specific fonts."""
from pathlib import Path
import math
import struct
import zlib

def png(path, size, menu=False):
    pixels = bytearray()
    for y in range(size):
        pixels.append(0)
        for x in range(size):
            px, py = (x + .5) / size - .5, (y + .5) / size - .5
            inside = math.hypot(max(abs(px) - .31, 0), max(abs(py) - .31, 0)) <= .16
            star = any(abs(px * math.sin(a) - py * math.cos(a)) < .028 and
                       abs(px * math.cos(a) + py * math.sin(a)) < .29
                       for a in (0, math.pi / 3, 2 * math.pi / 3))
            color = (255, 255, 255, 255) if star else ((217, 119, 69, 255) if inside else (0, 0, 0, 0))
            pixels.extend(color)
    def chunk(kind, data):
        return struct.pack('!I', len(data)) + kind + data + struct.pack('!I', zlib.crc32(kind + data))
    Path(path).write_bytes(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('!2I5B', size, size, 8, 6, 0, 0, 0)) +
                          chunk(b'IDAT', zlib.compress(pixels)) + chunk(b'IEND', b''))

png('src/AIUsageWidget.Desktop/Assets/tray.png', 64)
png('src/AIUsageWidget.Desktop/Assets/app.png', 1024)
