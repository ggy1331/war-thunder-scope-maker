"""Convert original WT AVIF backgrounds to lossless PNG for Windows GDI+."""
from pathlib import Path
import hashlib
import json
from PIL import Image

root=Path(__file__).resolve().parents[1]
source=root/'research'/'images.vromfs.bin'/'images'/'sight_menu_bg'
dest=root/'assets'/'backgrounds'
dest.mkdir(parents=True,exist_ok=True)
entries=[]
for scene in range(1,5):
    for mode in ('day','night','nightvision','thermal'):
        name=f'tank_sight_preview_{scene}_{mode}'
        original=source/(name+'.avif')
        thumbnail=source/f'tank_sight_preview_sm_{scene}_{mode}.avif'
        with Image.open(original) as image:
            rgb=image.convert('RGB')
            rgb.save(dest/(name+'.png'),optimize=True)
            # Verify conversion preserves every decoded pixel and native resolution.
            with Image.open(dest/(name+'.png')) as converted:
                assert converted.size==image.size and converted.tobytes()==rgb.tobytes()
            size=list(image.size)
        with Image.open(thumbnail) as image:image.convert('RGB').save(dest/(name+'_thumb.png'),optimize=True)
        entries.append({'Key':f'{scene}_{mode}','Scene':scene,'Mode':mode,'File':name+'.png','Thumbnail':name+'_thumb.png','Width':size[0],'Height':size[1],'Source':'ui/images/sight_menu_bg/'+name+'.avif','Sha256':hashlib.sha256(original.read_bytes()).hexdigest()})
(dest/'manifest.json').write_text(json.dumps(entries,indent=2),encoding='utf-8')
print(json.dumps({'backgrounds':len(entries),'resolution':size,'png_bytes':sum(p.stat().st_size for p in dest.glob('*.png'))}))
