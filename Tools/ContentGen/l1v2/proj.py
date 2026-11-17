"""Goal-image <-> world projection for L1 v2 (Tide Watchtower). Goal frame 3:4, vFOV 55 deg, pitch 12.3 deg down.
Screen coords: u,v in % of width/height from top-left. World: +x away from camera, +y up, +z = screen LEFT."""
import math
TV=math.tan(math.radians(27.5)); TH=TV*0.75
PITCH=math.atan(0.21*2*TV)            # horizon at v=29%
CAM=(-25.8,4.7,0.0)                   # 4.0 m behind the breech ring, 2.7 m over the barrel axis
SEA=-1.0
def ray(u,v):
    x=(2*u/100-1)*TH; y=(1-2*v/100)*TV
    cp,sp=math.cos(PITCH),math.sin(PITCH)
    # camera space (x right, y up, z fwd) -> world (fwd, up, left)
    fwd=cp*1-sp*y*-1*-1 if False else cp - (-sp)*0  # placeholder
    f=cp*1 + sp*y*0
    dx= cp*1 - sp*0
    # rotate (y,z) by pitch down
    wy= y*cp - 1*sp; wf= y*sp + 1*cp
    return (wf, wy, -x)
def on_plane(u,v,X):
    """point where the ray hits the vertical plane x=X (world)"""
    d=ray(u,v); t=(X-CAM[0])/d[0]
    return tuple(round(CAM[i]+t*d[i],2) for i in range(3))
def to_screen(p):
    dx,dy,dz=p[0]-CAM[0],p[1]-CAM[1],p[2]-CAM[2]
    cp,sp=math.cos(PITCH),math.sin(PITCH)
    z= dx*cp - dy*sp; y= dx*sp + dy*cp; x=-dz
    return (round(50+50*x/(z*TH),1), round(50-50*y/(z*TV),1))
if __name__=='__main__':
    print('pitch',round(math.degrees(PITCH),2),'hfov',round(2*math.degrees(math.atan(TH)),1))
    print('horizon v', to_screen((5000,SEA,0)))
