from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "store" / "rustore"
# Visual mockups are useful for layout experiments, but must never overwrite the
# real Android captures shipped to RuStore.
SHOTS = ROOT / "artifacts" / "store-mockups"
GRAPHICS = OUT / "graphics"
SHOTS.mkdir(parents=True, exist_ok=True)
GRAPHICS.mkdir(parents=True, exist_ok=True)

W, H = 1080, 1920
NAVY, SURFACE, RAISED = "#0B1020", "#151B2E", "#202841"
GOLD, IVORY, MUTED, BURGUNDY = "#D6AE68", "#F3E9D6", "#9DA7BE", "#6E183E"
FONT = Path(r"C:\Windows\Fonts\segoeui.ttf")
BOLD = Path(r"C:\Windows\Fonts\segoeuib.ttf")
SYMBOL = Path(r"C:\Windows\Fonts\seguisym.ttf")

def font(size, bold=False, symbol=False):
    return ImageFont.truetype(str(SYMBOL if symbol else BOLD if bold else FONT), size)

def rounded(draw, box, radius, fill, outline=None, width=1):
    draw.rounded_rectangle(box, radius=radius, fill=fill, outline=outline, width=width)

def text(draw, xy, value, size, fill=IVORY, bold=False, anchor=None):
    draw.text(xy, value, font=font(size, bold), fill=fill, anchor=anchor)

def wrap(draw, value, max_width, size, bold=False):
    words, lines, current = value.split(), [], ""
    for word in words:
        trial = (current + " " + word).strip()
        if draw.textbbox((0,0), trial, font=font(size,bold))[2] <= max_width: current = trial
        else: lines.append(current); current = word
    if current: lines.append(current)
    return lines

def button(draw, y, label, fill=GOLD, color="#101522"):
    rounded(draw, (72,y,W-72,y+112), 34, fill)
    text(draw, (W//2,y+56), label, 34, color, True, "mm")

def header(draw, title, subtitle):
    text(draw, (72,88), "Шахматы Velvet", 30, GOLD, True)
    text(draw, (72,154), title, 56, IVORY, True)
    text(draw, (72,226), subtitle, 27, MUTED)

def board(draw, x, y, size, fen):
    fields = fen.split()[0].split('/')
    pieces = {}
    glyphs = {'K':'♔','Q':'♕','R':'♖','B':'♗','N':'♘','P':'♙','k':'♚','q':'♛','r':'♜','b':'♝','n':'♞','p':'♟'}
    for row, rank in enumerate(fields):
        file = 0
        for c in rank:
            if c.isdigit(): file += int(c)
            else: pieces[(file,row)] = c; file += 1
    cell = size//8
    for row in range(8):
        for col in range(8):
            fill = "#E6D4B7" if (row+col)%2==0 else "#6E4051"
            draw.rectangle((x+col*cell,y+row*cell,x+(col+1)*cell,y+(row+1)*cell), fill=fill)
            p = pieces.get((col,row))
            if p:
                draw.text((x+col*cell+cell//2,y+row*cell+cell//2-3),glyphs[p],font=font(int(cell*.72),symbol=True),fill="#FFF9EB" if p.isupper() else "#111629",anchor="mm")

def save(img, name):
    img.save(SHOTS/name, optimize=True)

# Store icon, also consumed directly by MAUI.
icon = Image.new("RGB", (512,512), NAVY); d = ImageDraw.Draw(icon)
d.ellipse((66,66,446,446), fill=BURGUNDY)
d.text((256,255), "♞", font=font(330,symbol=True), fill=GOLD, anchor="mm")
icon.save(GRAPHICS/"app_icon_512.png", optimize=True)
icon.save(ROOT/"src"/"VelvetChess.App"/"Resources"/"AppIcon"/"appicon.png", optimize=True)

# 01 — home.
img = Image.new("RGB",(W,H),NAVY); d=ImageDraw.Draw(img)
art=Image.open(GRAPHICS/"brand_key_art_source.png").convert("RGB").resize((W,W))
img.paste(art.crop((0,80,W,760)),(0,0)); d=ImageDraw.Draw(img)
text(d,(72,814),"ВАША ПАРТИЯ. ВАШ ТЕМП.",25,GOLD,True)
for i,line in enumerate(wrap(d,"Красивые шахматы, которые всегда рядом",900,64,True)): text(d,(72,875+i*74),line,64,IVORY,True)
text(d,(72,1060),"Играйте и тренируйтесь полностью офлайн",30,MUTED)
button(d,1150,"Продолжить партию"); button(d,1290,"50 тактических задач",BURGUNDY,"#FFFFFF")
rounded(d,(72,1450,W-72,1690),28,SURFACE); text(d,(110,1500),"4 УРОВНЯ СЛОЖНОСТИ",24,GOLD,True)
for i,line in enumerate(wrap(d,"Решено 12/50 · Партий 7 · Побед 3",820,31)): text(d,(110,1550+i*42),line,31,MUTED)
save(img,"01_home.png")

# 02 — game.
img=Image.new("RGB",(W,H),NAVY); d=ImageDraw.Draw(img); header(d,"Локальная партия","Уровень: Любитель")
rounded(d,(54,290,W-54,1318),32,SURFACE); board(d,78,314,924,"rnbqkbnr/pppp1ppp/8/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq - 1 2")
text(d,(W//2,1370),"Ваш ход",32,IVORY,True,"mm"); text(d,(W//2,1420),"1. e4  e5   2. Nf3  Nc6",24,MUTED,False,"mm")
rounded(d,(72,1470,486,1582),34,GOLD); text(d,(279,1526),"Новая партия",30,"#101522",True,"mm")
rounded(d,(510,1470,W-72,1582),34,RAISED); text(d,(759,1526),"Отменить ход",30,"#FFFFFF",True,"mm")
text(d,(W//2,1675),"Автосохранение • Честные правила • Без рекламы",25,MUTED,False,"mm")
save(img,"02_local_game.png")

# 03 — list.
img=Image.new("RGB",(W,H),NAVY); d=ImageDraw.Draw(img); header(d,"Тактическая коллекция","50 проверенных позиций")
items=[("РЕШЕНО · Мат в 1 · 1",923),("РЕШЕНО · Вилка · 2",1204),("Отвлечение · 3",1421),("Эндшпиль · 4",1588),("Вскрытое нападение · 5",1712),("Завлечение · 6",1850)]
for i,(label,rating) in enumerate(items):
    y=300+i*230; rounded(d,(54,y,W-54,y+190),28,SURFACE); text(d,(94,y+50),label,38,IVORY,True); text(d,(94,y+112),f"Рейтинг {rating} · решите без подсказки",25,MUTED); text(d,(W-108,y+94),"›",54,GOLD,True,"mm")
save(img,"03_puzzles.png")

# 04 — puzzle.
img=Image.new("RGB",(W,H),NAVY); d=ImageDraw.Draw(img); header(d,"Вилка · 1","Найдите лучший ход")
rounded(d,(54,290,W-54,1318),32,SURFACE); board(d,78,314,924,"2r1r1k1/p4q1p/bp4pP/3R1N2/P1n5/4N3/4QPP1/2R3K1 b - - 1 29")
text(d,(72,1386),"Сложность: 1204",28,MUTED)
rounded(d,(72,1460,476,1572),34,RAISED); text(d,(274,1516),"Подсказка",28,"#FFFFFF",True,"mm")
rounded(d,(500,1460,W-72,1572),34,RAISED); text(d,(754,1516),"Показать решение",25,"#FFFFFF",True,"mm")
text(d,(W//2,1660),"Шахи • Взятия • Угрозы",28,GOLD,True,"mm")
save(img,"04_puzzle_play.png")

# 05 — settings and privacy.
img=Image.new("RGB",(W,H),NAVY); d=ImageDraw.Draw(img); header(d,"Настройки","Комфортная игра без лишнего")
settings=[("Координаты доски","Буквы и цифры у полей",True),("Тактильный отклик","Короткий отклик после хода",True),("Подтверждать новую партию","Защита текущей позиции",True)]
for i,(label,desc,on) in enumerate(settings):
    y=310+i*220; rounded(d,(54,y,W-54,y+180),28,SURFACE); text(d,(94,y+44),label,34,IVORY,True); text(d,(94,y+103),desc,24,MUTED)
    rounded(d,(W-220,y+57,W-94,y+123),33,GOLD if on else RAISED); d.ellipse((W-157,y+65,W-101,y+121),fill="#FFFFFF")
text(d,(72,1010),"ВАШИ ДАННЫЕ",24,GOLD,True)
rounded(d,(54,1060,W-54,1365),28,SURFACE)
privacy="Игра работает офлайн. Персональные данные, реклама и аналитические идентификаторы не собираются. Партии и прогресс хранятся только на устройстве."
for i,line in enumerate(wrap(d,privacy,850,29)): text(d,(94,1110+i*43),line,29,MUTED)
button(d,1435,"Сбросить прогресс",BURGUNDY,"#FFFFFF")
text(d,(W//2,1650),"Шахматы Velvet · версия 1.0.0",24,MUTED,False,"mm")
save(img,"05_settings_privacy.png")

# 06 — solved puzzle with the complete line.
img=Image.new("RGB",(W,H),NAVY); d=ImageDraw.Draw(img); header(d,"Задача решена","Точная линия найдена")
rounded(d,(54,290,W-54,1318),32,SURFACE); board(d,78,314,924,"2r1r3/p4k1p/bp4pP/3N4/P1n5/4N3/4QPP1/2R3K1 b - - 0 30")
text(d,(72,1365),"РЕШЕНИЕ",24,GOLD,True); text(d,(72,1415),"29. Nf6+  Kf7  30. Nxd5",32,IVORY,True)
for i,line in enumerate(wrap(d,"Вилка: конь атакует короля и ферзя, выигрывая материал.",880,26)): text(d,(72,1470+i*40),line,26,MUTED)
button(d,1640,"Следующая задача",GOLD,"#101522")
save(img,"06_puzzle_solution.png")

print(f"Rendered icon and 6 non-production mockups in {SHOTS}")
