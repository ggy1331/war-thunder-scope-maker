// Read-only inspection of local VROMFS containers. Outputs stay inside research/.
// Format references: klensy/wt-tools formats/vromfs_parser.py and vromfs_unpacker.py.
const fs = require('node:fs');
const path = require('node:path');
const zlib = require('node:zlib');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '..', 'research');
fs.mkdirSync(root, { recursive: true });
for (const filename of process.argv.slice(2)) {
 const bytes = fs.readFileSync(filename), magic = bytes.toString('ascii', 0, 4);
 if (!['VRFx', 'VRFs'].includes(magic)) throw Error('Unknown VROMFS header');
 const rawSize = bytes.readUInt32LE(8), packed = bytes.readUInt32LE(12) & 0x3fffffff, type = bytes[15] & 0xc0;
 const offset = magic === 'VRFx' ? 16 + bytes.readUInt16LE(16) : 16;
 let body;
 if (type === 0xc0 || type === 0x40) {
  const buf = Buffer.from(bytes.subarray(offset, offset + packed));
  for (let j=0; j<4; j++) buf.writeUInt32LE((buf.readUInt32LE(j*4) ^ [0xaa55aa55,0xf00ff00f,0xaa55aa55,0x12481248][j]) >>> 0,j*4);
  if (packed>=32) {const last=Math.floor((packed-32)/4)*4+16; for(let j=0;j<4;j++)buf.writeUInt32LE((buf.readUInt32LE(last+j*4)^[0x12481248,0xaa55aa55,0xf00ff00f,0xaa55aa55][j])>>>0,last+j*4);}
  body=zlib.zstdDecompressSync(buf,{maxOutputLength:rawSize});
 } else body = packed ? zlib.inflateSync(bytes.subarray(offset,offset+packed)) : bytes.subarray(offset,offset+rawSize);
 if(body.length!==rawSize)throw Error('Unpacked length mismatch');
 const count=body.readUInt32LE(4),table=body.readUInt32LE(16),names=body.readUInt32LE(0);
 let nameOffset=body.readUInt32LE(names);const files=[];
 for(let i=0;i<count;i++){
  const end=body.indexOf(0,nameOffset);let name=body.toString('utf8',nameOffset,end);if(name.includes('?nm'))name='nm';nameOffset=end+1;
  const at=table+i*16,start=body.readUInt32LE(at),size=body.readUInt32LE(at+4);files.push({name,start,size});
 }
 const filter=new RegExp(process.env.WT_RESOURCE_PATTERN||'sight|crosshair|reticle','i');
 const matches=files.filter(f=>filter.test(f.name));
 const archive=path.basename(filename);const dest=path.join(root,archive);fs.mkdirSync(dest,{recursive:true});
 fs.writeFileSync(path.join(dest,'index.json'),JSON.stringify({archive,sha256:crypto.createHash('sha256').update(bytes).digest('hex'),rawSize,count,matches},null,2));
 // Save only sight resources and decoding metadata; never write into the game folder.
 for(const f of files.filter(f=>matches.includes(f)||f.name==='nm'||f.name.endsWith('.dict'))){
  const safe=path.resolve(dest,...f.name.split(/[\\/]/));if(!safe.startsWith(dest+path.sep))throw Error('Unsafe archive path');
  fs.mkdirSync(path.dirname(safe),{recursive:true});fs.writeFileSync(safe,body.subarray(f.start,f.start+f.size));
 }
 console.log(JSON.stringify({archive,files:count,matches:matches.map(f=>f.name)}));
}
