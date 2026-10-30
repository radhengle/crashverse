import sys
from PIL import Image
files=sys.argv[2:]; out=sys.argv[1]
W=len(files)*266+10
im=Image.new('RGB',(W,532),(255,255,255))
im.paste((16,24,48),(0,0,W,266))
for i,f in enumerate(files):
    ic=Image.open(f).convert('RGBA')
    im.paste(ic,(10+i*266,5),ic); im.paste(ic,(10+i*266,271),ic)
im.save(out)
