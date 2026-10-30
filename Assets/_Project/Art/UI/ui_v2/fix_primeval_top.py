"""Remove the large pterosaur that crossed the 158px top safe band (cv2 inpaint over plain sky)."""
import numpy as np, cv2
im=cv2.imread('bg_season_primeval_peaks.png')
x0,y0,x1,y1=60,0,360,300
roi=im[y0:y1,x0:x1].astype(int)
b,g,r=roi[...,0],roi[...,1],roi[...,2]
m=((r>b-10)|(b-r<40)).astype(np.uint8)*255   # non-sky pixels
m=cv2.dilate(m,np.ones((9,9),np.uint8))
mask=np.zeros(im.shape[:2],np.uint8); mask[y0:y1,x0:x1]=m
print('masked px',(mask>0).sum())
out=cv2.inpaint(im,mask,9,cv2.INPAINT_TELEA)
cv2.imwrite('bg_season_primeval_peaks.png',out)
cv2.imwrite('/tmp/pp_top.png',out[0:500,0:600])
