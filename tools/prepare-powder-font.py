from io import BytesIO
from pathlib import Path
import struct
from fontTools.ttLib import TTFont
from fontTools.pens.recordingPen import DecomposingRecordingPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.pens.transformPen import TransformPen

root=Path(__file__).resolve().parents[1]
sourcePath=root/'tools'/'font-source'
wrapperPath=sourcePath/'jf-openhuninn-2.1.dat'
wrapper=wrapperPath.read_bytes()
ttfLength=struct.unpack_from('<i',wrapper,88)[0]
ttfStart=92
originalEnd=(ttfStart+ttfLength+3)&~3
font=TTFont(BytesIO(wrapper[ttfStart:ttfStart+ttfLength]))
fallback=TTFont(sourcePath/'NotoSansTC-Regular.ttf')
fontCmap=font.getBestCmap()
fallbackCmap=fallback.getBestCmap()

def is_cjk(cp):
    return (0x3000<=cp<=0x303f or 0x3400<=cp<=0x9fff or
            0xf900<=cp<=0xfaff or 0xff00<=cp<=0xffef or
            0x20000<=cp<=0x323af)

chars={cp for cp in fallbackCmap if is_cjk(cp)}
missing=chars-set(fontCmap)
glyphset=fallback.getGlyphSet()
ratio=font['head'].unitsPerEm/fallback['head'].unitsPerEm
order=list(font.getGlyphOrder())
for cp in sorted(missing):
    original=fallbackCmap[cp]
    name=f'fallback_u{cp:04X}'
    recorder=DecomposingRecordingPen(glyphset)
    glyphset[original].draw(recorder)
    pen=TTGlyphPen(None)
    recorder.replay(TransformPen(pen,(ratio,0,0,ratio,0,0)))
    font['glyf'][name]=pen.glyph()
    font['hmtx'][name]=tuple(round(x*ratio) for x in fallback['hmtx'][original])
    if 'vmtx' in font:
        font['vmtx'][name]=tuple(round(x*ratio) for x in fallback['vmtx'][original])
    order.append(name)
    for table in font['cmap'].tables:
        if table.isUnicode() and (table.format in (4,6,10) and cp<=0xffff or table.format in (12,13)):
            table.cmap[cp]=name
font.setGlyphOrder(order)
if 'DSIG' in font: del font['DSIG']
values={1:'LE Chinese Rounded',2:'Regular',3:'LEChineseRounded-Regular-2.1-local',
        4:'LE Chinese Rounded Regular',6:'LEChineseRounded-Regular',
        16:'LE Chinese Rounded',17:'Regular'}
for record in font['name'].names:
    if record.nameID in values:
        record.string=values[record.nameID].encode(record.getEncoding(),errors='replace')
out=BytesIO()
font.save(out)
expanded=out.getvalue()
result=TTFont(BytesIO(expanded))
assert chars<=set(result.getBestCmap())
rebuilt=wrapper[:88]+struct.pack('<i',len(expanded))+expanded+b'\0'*((-len(expanded))%4)+wrapper[originalEnd:]
destination=root/'LETraditionalChinese'/'fonts'/'jf-openhuninn-2.1.dat'
destination.write_bytes(rebuilt)
print(f'Added {len(missing)} Noto Sans TC CJK glyphs ({len(chars)} covered); embedded font {ttfLength:,} -> {len(expanded):,} bytes; preserved {len(wrapper[originalEnd:])} wrapper tail bytes.')
