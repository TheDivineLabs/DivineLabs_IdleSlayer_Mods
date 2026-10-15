"""Extract original icon resources from the user's installed game; no game binaries are copied."""
import struct, sys
from pathlib import Path
data=Path(sys.argv[1]).read_bytes()
pe=struct.unpack_from("<I",data,0x3c)[0]
sections=struct.unpack_from("<H",data,pe+6)[0]
opt_size=struct.unpack_from("<H",data,pe+20)[0]
opt=pe+24
magic=struct.unpack_from("<H",data,opt)[0]
directory=opt+(112 if magic==0x20b else 96)
resource_rva=struct.unpack_from("<I",data,directory+16)[0]
table=opt+opt_size
regions=[]
for i in range(sections):
    offset=table+40*i
    virtual_size,virtual_address,raw_size,raw_address=struct.unpack_from("<IIII",data,offset+8)
    regions.append((virtual_address,max(virtual_size,raw_size),raw_address))
def raw(rva):
    for va,size,address in regions:
        if va<=rva<va+size:return address+rva-va
    raise ValueError("Invalid resource RVA")
base=raw(resource_rva)
def entries(relative):
    p=base+relative
    named,ids=struct.unpack_from("<HH",data,p+12)
    return [struct.unpack_from("<II",data,p+16+8*i) for i in range(named+ids)]
def leaf(relative):
    for _,offset in entries(relative):
        if offset&0x80000000:return leaf(offset&0x7fffffff)
        rva,size=struct.unpack_from("<II",data,base+offset)
        return data[raw(rva):raw(rva)+size]
    raise ValueError("Empty resource")
types={name:offset&0x7fffffff for name,offset in entries(0)}
icons={name:leaf(offset&0x7fffffff) for name,offset in entries(types[3])}
group=leaf(entries(types[14])[0][1]&0x7fffffff)
count=struct.unpack_from("<H",group,4)[0]
header=bytearray(struct.pack("<HHH",0,1,count))
payload=bytearray()
for i in range(count):
    width,height,colors,reserved,planes,bits,size,identifier=struct.unpack_from("<BBBBHHIH",group,6+14*i)
    image=icons[identifier]
    header+=struct.pack("<BBBBHHII",width,height,colors,0,planes,bits,len(image),6+16*count+len(payload))
    payload+=image
Path(sys.argv[2]).write_bytes(header+payload)
print("Original Idle Slayer icon extracted:",count,"sizes")
