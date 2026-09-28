from PIL import Image, ImageDraw
import os

W = H = 64
img = Image.new("RGBA", (W, H), (0,0,0,0))
d = ImageDraw.Draw(img)

# 背景：圆形沙漠渐变（暖黄棕）
# 外圈深棕
d.ellipse([4,4,60,60], fill=(194,142,76,255))   # 土黄
d.ellipse([8,8,56,56], fill=(222,178,110,255))  # 沙黄

# 地面焦土横线（干裂感）
for y in [44, 50]:
    d.line([(14,y),(50,y)], fill=(140,95,50,255), width=1)
d.line([(22,44),(22,50)], fill=(140,95,50,255), width=1)
d.line([(40,44),(40,50)], fill=(140,95,50,255), width=1)

# 枯萎水滴（中心，淡蓝→灰蓝，干裂缺水）
drop = [(32,16),(24,30),(24,38),(32,44),(40,38),(40,30)]
d.polygon(drop, fill=(150,170,180,255))
# 水滴裂痕
d.line([(32,24),(30,32)], fill=(90,100,110,255), width=1)
d.line([(30,32),(34,36)], fill=(90,100,110,255), width=1)

# 热浪线（上方波浪，表示干燥热风）
for i, y in enumerate([10, 12]):
    pts = [(14+i*4, y),(20+i*4, y-2),(26+i*4, y),(32+i*4, y-2),(38+i*4, y),(44+i*4, y-2)]
    d.line(pts, fill=(240,220,160,255), width=1)

out = r"D:\DoubaoWork\Project_001_WagesPerks\07_开发资产\ProbablyStolen_DevFiles\Mods_开发源码与临时文件\JacksonPerks\24_干燥空气.png"
img.save(out)
print("saved", out, os.path.getsize(out))
