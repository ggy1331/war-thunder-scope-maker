"""Decode the locally extracted SLIM tank-sight BLK; no game-folder writes.

Format references (independent implementation): WOSF/wt_blk name_map.rs,
binary_deserialize/parser.rs, blk_type.rs. Uses bundled libzstd via ctypes.
"""
import ctypes as c
import json
from pathlib import Path
import struct
import sys

root = Path(__file__).resolve().parents[1] / 'research' / 'aces.vromfs.bin'
lib = c.CDLL(sys.argv[1])
lib.ZSTD_createDCtx.restype = c.c_void_p
lib.ZSTD_freeDCtx.argtypes = [c.c_void_p]
lib.ZSTD_decompress_usingDict.argtypes = [c.c_void_p,c.c_void_p,c.c_size_t,c.c_void_p,c.c_size_t,c.c_void_p,c.c_size_t]
lib.ZSTD_decompress_usingDict.restype = c.c_size_t
lib.ZSTD_isError.argtypes = [c.c_size_t]
lib.ZSTD_getErrorName.argtypes = [c.c_size_t]
lib.ZSTD_getErrorName.restype = c.c_char_p

def decompress(data, dictionary=b''):
    out = c.create_string_buffer(32*1024*1024)
    context = lib.ZSTD_createDCtx()
    try:
        size=lib.ZSTD_decompress_usingDict(context,out,len(out),data,len(data),dictionary,len(dictionary))
        if lib.ZSTD_isError(size): raise ValueError(lib.ZSTD_getErrorName(size))
        return out.raw[:size]
    finally: lib.ZSTD_freeDCtx(context)

class Reader:
    def __init__(self,data): self.data,self.offset=data,0
    def read(self,length):
        value=self.data[self.offset:self.offset+length]
        if len(value)!=length: raise ValueError('Truncated BLK')
        self.offset+=length
        return value
    def leb(self):
        value,shift=0,0
        while True:
            byte=self.read(1)[0];value|=(byte&127)<<shift
            if not byte&128:return value
            shift+=7
            if shift>35:raise ValueError('Invalid LEB128')

names_data=decompress((root/'nm').read_bytes()[40:])
nr=Reader(names_data)
names_count=nr.leb();names_length=nr.leb()
names=nr.read(names_length).decode('utf-8').split('\0')[:-1]
assert len(names)==names_count
raw=(root/'config/tanksights.blk').read_bytes()
assert raw[0]==5
body=decompress(raw[1:],next(root.glob('*.dict')).read_bytes())
r=Reader(body)
assert r.leb()==0
block_count,param_count,data_size=r.leb(),r.leb(),r.leb()
values=r.read(data_size)
parameters=r.read(param_count*8)

def parameter(index):
    info=parameters[index*8:index*8+8]
    name=names[int.from_bytes(info[:3],'little')];kind=info[3]
    value=info[4:];offset=int.from_bytes(value,'little')
    if kind==1:
        text=names[offset&0x7fffffff] if offset&0x80000000 else names_data[offset:names_data.index(b'\0',offset)].decode('utf-8')
        return name,'t',json.dumps(text,ensure_ascii=False)
    if kind==2:return name,'i',str(struct.unpack('<i',value)[0])
    if kind==3:return name,'r',format(struct.unpack('<f',value)[0],'.9g')
    if kind==9:return name,'b','yes' if offset else 'no'
    if kind==10:return name,'c',', '.join(str(x) for x in value)
    counts={4:('p2',2,'f'),5:('p3',3,'f'),6:('p4',4,'f'),7:('ip2',2,'i'),8:('ip3',3,'i'),13:('ip4',4,'i')}
    if kind in counts:
        tag,count,fmt=counts[kind]
        return name,tag,', '.join(format(x,'.9g') if fmt=='f' else str(x) for x in struct.unpack_from('<'+fmt*count,values,offset))
    raise ValueError(f'Unsupported parameter type {kind}')

blocks=[];param_offset=0
for i in range(block_count):
    name_id,count,children=r.leb(),r.leb(),r.leb()
    first=r.leb() if children else 0
    blocks.append({'name':names[name_id-1] if name_id else 'root','params':[parameter(j) for j in range(param_offset,param_offset+count)],'children':list(range(first,first+children))})
    param_offset+=count
assert param_offset==param_count and r.offset==len(body)

visited=set()
def dump(index,depth=0):
    if index in visited:raise ValueError('Repeated block')
    visited.add(index);block=blocks[index];indent='  '*depth
    return ''.join(indent+name+':'+kind+' = '+value+';\n' for name,kind,value in block['params'])+''.join(indent+blocks[child]['name']+' {\n'+dump(child,depth+1)+indent+'}\n' for child in block['children'])

text=dump(0)
assert len(visited)==block_count
(root/'config/tanksights.decoded.blk').write_text(text,encoding='utf-8')
(root/'config/tanksights.tree.json').write_text(json.dumps(blocks,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'blocks':block_count,'parameters':param_count,'sights':[blocks[i]['name'] for i in blocks[0]['children']]},ensure_ascii=False))

library=Path(__file__).resolve().parents[1]/'templates'/'game'
library.mkdir(parents=True,exist_ok=True)
entries=[]
for group_id in blocks[0]['children']:
    group=blocks[group_id]
    if group['name']=='tankSightPresets':continue
    for sight_id in group['children']:
        sight=blocks[sight_id];visited.clear();content=dump(sight_id)
        if group['name']!='all_tanks' and not any(blocks[i]['name']=='matchExpClass' for i in sight['children']):
            content+='matchExpClass { '+group['name']+':b = yes; }\n'
        name=group['name']+'__'+sight['name']+'.blk'
        (library/name).write_text('// Extracted from local War Thunder config/tanksights.blk; game 2.59.0.86\n'+content,encoding='utf-8')
        entries.append({'File':name,'Name':sight['name'],'Group':group['name']})
(library/'manifest.json').write_text(json.dumps(entries,ensure_ascii=False,indent=2),encoding='utf-8')
print('Extracted template files:',len(entries))
