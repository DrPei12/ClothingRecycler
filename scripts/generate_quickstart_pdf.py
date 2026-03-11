from __future__ import annotations

from pathlib import Path

from PIL import Image as PILImage
from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.cidfonts import UnicodeCIDFont
from reportlab.platypus import (
    Image,
    PageBreak,
    Paragraph,
    SimpleDocTemplate,
    Spacer,
    Table,
    TableStyle,
)


ROOT = Path(__file__).resolve().parents[1]
OUTPUT_DIR = ROOT / "output" / "pdf"
PDF_PATH = OUTPUT_DIR / "ClothingRecycler_QuickStart_v1.0.0.pdf"
ASSET_CACHE_DIR = ROOT / "tmp" / "pdfs" / "quickstart-assets"

LOGO_PATH = Path(r"D:\Desktop\logo.png")
INSTALLER_PATH = ROOT / "artifacts" / "release" / "ClothingRecycler_PC_Install_v1.0.0.exe"

SCREENSHOT_INBOUND = ROOT / "docs" / "screenshots" / "screenshot-inbound.png"
SCREENSHOT_ORDERS = ROOT / "docs" / "screenshots" / "screenshot-orders.png"
SCREENSHOT_STOCK = ROOT / "docs" / "screenshots" / "screenshot-stock.png"
SCREENSHOT_ANALYTICS = ROOT / "docs" / "screenshots" / "screenshot-analytics.png"


def register_fonts() -> None:
    pdfmetrics.registerFont(UnicodeCIDFont("STSong-Light"))


def build_styles():
    styles = getSampleStyleSheet()
    base_font = "STSong-Light"

    title = ParagraphStyle(
        "TitleCn",
        parent=styles["Title"],
        fontName=base_font,
        fontSize=24,
        leading=30,
        textColor=colors.HexColor("#111827"),
        alignment=TA_CENTER,
        spaceAfter=8,
    )
    subtitle = ParagraphStyle(
        "SubtitleCn",
        parent=styles["BodyText"],
        fontName=base_font,
        fontSize=11,
        leading=16,
        textColor=colors.HexColor("#4B5563"),
        alignment=TA_CENTER,
        spaceAfter=8,
    )
    heading = ParagraphStyle(
        "HeadingCn",
        parent=styles["Heading2"],
        fontName=base_font,
        fontSize=16,
        leading=22,
        textColor=colors.HexColor("#111827"),
        spaceBefore=8,
        spaceAfter=8,
    )
    body = ParagraphStyle(
        "BodyCn",
        parent=styles["BodyText"],
        fontName=base_font,
        fontSize=10.5,
        leading=16,
        textColor=colors.HexColor("#1F2937"),
        spaceAfter=5,
    )
    small = ParagraphStyle(
        "SmallCn",
        parent=styles["BodyText"],
        fontName=base_font,
        fontSize=9,
        leading=13,
        textColor=colors.HexColor("#6B7280"),
        spaceAfter=4,
    )
    caption = ParagraphStyle(
        "CaptionCn",
        parent=styles["BodyText"],
        fontName=base_font,
        fontSize=9.5,
        leading=13,
        textColor=colors.HexColor("#4B5563"),
        alignment=TA_CENTER,
        spaceBefore=4,
        spaceAfter=10,
    )
    card_title = ParagraphStyle(
        "CardTitleCn",
        parent=styles["BodyText"],
        fontName=base_font,
        fontSize=11,
        leading=14,
        textColor=colors.HexColor("#111827"),
        alignment=TA_CENTER,
    )
    card_value = ParagraphStyle(
        "CardValueCn",
        parent=styles["BodyText"],
        fontName=base_font,
        fontSize=9.5,
        leading=13,
        textColor=colors.HexColor("#4B5563"),
        alignment=TA_CENTER,
    )
    return {
        "title": title,
        "subtitle": subtitle,
        "heading": heading,
        "body": body,
        "small": small,
        "caption": caption,
        "card_title": card_title,
        "card_value": card_value,
    }


def paragraph(text: str, style: ParagraphStyle) -> Paragraph:
    return Paragraph(text.replace("\n", "<br/>"), style)


def build_image(path: Path, max_width_mm: float) -> Image:
    with PILImage.open(path) as image:
        width, height = image.size

    target_width = max_width_mm * mm
    target_height = target_width * (height / width)
    return Image(str(path), width=target_width, height=target_height)


def prepare_screenshot(path: Path, cache_name: str) -> Path:
    ASSET_CACHE_DIR.mkdir(parents=True, exist_ok=True)
    output_path = ASSET_CACHE_DIR / cache_name

    with PILImage.open(path) as image:
        width, height = image.size
        left = int(width * 0.19)
        top = int(height * 0.165)
        right = width
        bottom = height
        cropped = image.crop((left, top, right, bottom))
        cropped.save(output_path)

    return output_path


def info_cards(styles: dict[str, ParagraphStyle]) -> Table:
    data = [[
        paragraph("纯本地运行", styles["card_title"]),
        paragraph("支持库存联动", styles["card_title"]),
        paragraph("支持订单回滚", styles["card_title"]),
        paragraph("支持备份恢复", styles["card_title"]),
    ], [
        paragraph("不依赖服务器", styles["card_value"]),
        paragraph("入库出库自动更新库存", styles["card_value"]),
        paragraph("编辑删除后自动修正派生数据", styles["card_value"]),
        paragraph("更适合固定电脑长期使用", styles["card_value"]),
    ]]

    table = Table(data, colWidths=[42 * mm] * 4)
    table.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, -1), colors.HexColor("#F9FAFB")),
        ("BOX", (0, 0), (-1, -1), 0.6, colors.HexColor("#D1D5DB")),
        ("INNERGRID", (0, 0), (-1, -1), 0.5, colors.HexColor("#E5E7EB")),
        ("TOPPADDING", (0, 0), (-1, -1), 8),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 8),
        ("LEFTPADDING", (0, 0), (-1, -1), 6),
        ("RIGHTPADDING", (0, 0), (-1, -1), 6),
        ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
    ]))
    return table


def note_box(title: str, lines: list[str], styles: dict[str, ParagraphStyle]) -> Table:
    body = [paragraph(title, styles["heading"])]
    for index, line in enumerate(lines, start=1):
        body.append(paragraph(f"{index}. {line}", styles["body"]))

    table = Table([[body]], colWidths=[170 * mm])
    table.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, -1), colors.HexColor("#F5F9FF")),
        ("BOX", (0, 0), (-1, -1), 0.8, colors.HexColor("#BFDBFE")),
        ("TOPPADDING", (0, 0), (-1, -1), 8),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 8),
        ("LEFTPADDING", (0, 0), (-1, -1), 10),
        ("RIGHTPADDING", (0, 0), (-1, -1), 10),
    ]))
    return table


def add_page_number(canvas, doc) -> None:
    canvas.saveState()
    canvas.setFont("STSong-Light", 9)
    canvas.setFillColor(colors.HexColor("#6B7280"))
    canvas.drawRightString(A4[0] - doc.rightMargin, 9 * mm, f"第 {canvas.getPageNumber()} 页")
    canvas.restoreState()


def build_story(styles: dict[str, ParagraphStyle]):
    inbound_image = prepare_screenshot(SCREENSHOT_INBOUND, "inbound-cropped.png")
    orders_image = prepare_screenshot(SCREENSHOT_ORDERS, "orders-cropped.png")
    stock_image = prepare_screenshot(SCREENSHOT_STOCK, "stock-cropped.png")
    analytics_image = prepare_screenshot(SCREENSHOT_ANALYTICS, "analytics-cropped.png")

    story = []

    if LOGO_PATH.exists():
        story.append(Image(str(LOGO_PATH), width=26 * mm, height=26 * mm))
        story.append(Spacer(1, 8))

    story.append(paragraph("衣物回收管理 PC 版", styles["title"]))
    story.append(paragraph("快速上手手册", styles["title"]))
    story.append(paragraph("适合发给最终使用者的安装与入门说明。当前版本 1.0.0。", styles["subtitle"]))
    story.append(Spacer(1, 6))
    story.append(info_cards(styles))
    story.append(Spacer(1, 10))
    story.append(note_box(
        "建议先看这三件事",
        [
            "第一次使用请先进入“设置”创建衣物分类。",
            "日常操作主要集中在“入库”、“出库”、“库存”和“订单”。",
            "每周至少做一次备份，换电脑或升级前先备份。",
        ],
        styles,
    ))
    story.append(Spacer(1, 8))
    story.append(paragraph(
        f"推荐安装文件：{INSTALLER_PATH}<br/>"
        "安装器支持自定义安装目录。安装完成后，会自动创建桌面快捷方式和开始菜单入口。",
        styles["small"],
    ))
    story.append(PageBreak())

    story.append(paragraph("一、安装与第一次使用", styles["heading"]))
    story.append(paragraph(
        "安装步骤很简单：双击安装包，先选择安装目录，再按提示完成安装，然后从桌面图标打开应用。"
        "第一次打开后，建议先在“设置”里建立分类，例如夏装、冬装、鞋子、包等，并为每个分类设置单位。",
        styles["body"],
    ))
    story.append(paragraph(
        "如果你是第一次开始记账，建议使用这个顺序：先建分类，再录第一批入库，之后再录出库。"
        "这样库存、订单和客户数据会自动建立起来。",
        styles["body"],
    ))
    story.append(build_image(inbound_image, 160))
    story.append(paragraph("图 1：入库工作台。录入分类、数量、单价和客户后，系统会自动生成订单并更新库存。", styles["caption"]))
    story.append(note_box(
        "第一次使用的推荐流程",
        [
            "先建分类，没分类就无法规范记账。",
            "有第一批货时先录入库，系统会自动生成库存。",
            "发生销售或流出时再录出库，避免账本顺序混乱。",
        ],
        styles,
    ))
    story.append(PageBreak())

    story.append(paragraph("二、日常使用怎么走", styles["heading"]))
    story.append(paragraph(
        "日常操作通常围绕四个页面：入库、订单、库存、经营分析。"
        "入库和出库负责录数据，订单负责改错和查找记录，库存负责盘点和调整，经营分析负责查看整体结果。",
        styles["body"],
    ))
    story.append(build_image(orders_image, 150))
    story.append(paragraph("图 2：订单中心。录错订单时，可以在这里查看、编辑或删除。", styles["caption"]))
    story.append(build_image(stock_image, 150))
    story.append(paragraph("图 3：库存中心。可以查看当前库存、盘点差异、低库存提醒和手动调整记录。", styles["caption"]))
    story.append(paragraph(
        "建议养成当天入库当天录、当天出库当天记的习惯。这样统计结果会更接近真实情况，也更容易发现问题。",
        styles["body"],
    ))
    story.append(PageBreak())

    story.append(paragraph("三、经营查看与数据安全", styles["heading"]))
    story.append(paragraph(
        "这套应用当前是纯本地运行的，不依赖服务器，也不主动上传数据。"
        "你的业务数据默认保存在 Windows 用户目录中，因此卸载应用不会删除账本。",
        styles["body"],
    ))
    story.append(build_image(analytics_image, 160))
    story.append(paragraph("图 4：经营分析页。可以查看累计入库、累计出库、客户贡献和分类排行。", styles["caption"]))
    story.append(note_box(
        "建议保留的数据习惯",
        [
            "每周至少手动备份一次。",
            "批量改数据前先备份一次。",
            "换电脑前先导出或复制备份目录。",
            "如果应用异常退出，优先查看日志目录和最近备份。",
        ],
        styles,
    ))
    story.append(Spacer(1, 8))
    story.append(paragraph(
        "默认数据目录：%LOCALAPPDATA%\\ClothingRecycler<br/>"
        "数据库：%LOCALAPPDATA%\\ClothingRecycler\\clothingrecycler.db<br/>"
        "备份：%LOCALAPPDATA%\\ClothingRecycler\\backups<br/>"
        "导出：%LOCALAPPDATA%\\ClothingRecycler\\exports<br/>"
        "日志：%LOCALAPPDATA%\\ClothingRecycler\\logs",
        styles["small"],
    ))
    story.append(PageBreak())

    story.append(paragraph("四、常见问题", styles["heading"]))
    faq_items = [
        ("安装后打不开怎么办？", "先确认系统是 Windows 10/11 64 位，再检查是否被安全软件拦截。必要时查看日志目录。"),
        ("卸载后数据会不会被删？", "不会。卸载只删除程序文件，不删除数据库、备份、导出和日志。"),
        ("录错订单怎么办？", "去订单中心找到对应订单，直接编辑或删除，系统会自动回滚相关派生数据。"),
        ("能不能和安卓实时同步？", "当前版本还不支持，现阶段优先保证 Windows 本地版稳定可用。"),
    ]
    for question, answer in faq_items:
        story.append(paragraph(question, styles["heading"]))
        story.append(paragraph(answer, styles["body"]))

    story.append(Spacer(1, 4))
    story.append(note_box(
        "交付建议",
        [
            "把安装包和这份 PDF 一起发给使用者。",
            "第一次交付时，最好同时附上 README_BEGINNER.md。",
            "如果是固定办公电脑，建议每周检查一次备份目录。",
        ],
        styles,
    ))
    return story


def main() -> None:
    register_fonts()
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    styles = build_styles()
    doc = SimpleDocTemplate(
        str(PDF_PATH),
        pagesize=A4,
        leftMargin=18 * mm,
        rightMargin=18 * mm,
        topMargin=18 * mm,
        bottomMargin=16 * mm,
        title="衣物回收管理 PC 版 - 快速上手手册",
        author="OpenAI Codex",
    )
    doc.build(build_story(styles), onFirstPage=add_page_number, onLaterPages=add_page_number)
    print(PDF_PATH)


if __name__ == "__main__":
    main()
