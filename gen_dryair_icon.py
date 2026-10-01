from PIL import Image, ImageDraw
import os

W = H = 64
img = Image.new("RGBA", (W, H), (0,0,0,0))
d = ImageDraw.Draw(img)
WHITE = (255,255,255,255)

# 枯萎水滴（白色轮廓+裂痕）
drop = [(32,14),(22,30),(22,40),(32,47),(42,40),(42,30)]
d.polygon(drop, outline=WHITE, width=2)
# 裂痕
d.line([(32,24),(29,33)], fill=WHITE, width=2)
d.line([(29,33),(35,38)], fill=WHITE, width=2)
# 地面焦土虚线
d.line([(14,54),(50,54)], fill=WHITE, width=1)
d.line([(24,54),(24,58)], fill=WHITE, width=1)
d.line([(40,54),(40,58)], fill=WHITE, width=1)
# 热浪波浪（上方）
for y in [8, 11]:
    pts = [(16,y),(21,y-2),(26,y),(31,y-2),(36,y),(41,y-2),(46,y)]
    d.line(pts, fill=WHITE, width=1)

out = r"D:\DoubaoWork\Project_001_WagesPerks\07_开发资产\ProbablyStolen_DevFiles\Mods_开发源码与临时文件\WagePerks\Icons\24_干燥空气.png"
img.save(out)
print("saved", out, os.path.getsize(out))
