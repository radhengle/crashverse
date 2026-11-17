from proj import *
# name: (u0,v0,u1,v1, plane_x, note)
E = {
 # central fortress
 'turret_roof':(51,2,60,5,-2.0,'small pagoda turret roof, red tiles, gold upturned corners, finial+flag'),
 'turret_body':(52.5,5,58,8,-2.0,'open timber lantern room, 4 posts'),
 'main_roof':(39,8.5,71,14,-5.0,'4-sided red tiled hip roof, upturned gold horn corners, 5 tile rows visible'),
 'dormer_L':(45,8.5,50,12,-4.6,'small gable dormer with window'),
 'dormer_R':(61,8,65,10.5,-3.6,'small gable dormer'),
 'gallery_room':(42,14,68,18.5,-4.3,'timber frame, 3 lit window bays (orange fire glow)'),
 'gallery_deck':(37.5,18.5,70,21.5,-5.2,'thick beam balcony with bolted plates, brackets below, railing on top'),
 'stone_body_top':(41,21.5,67,24,-4.5,'stone tower, top width'),
 'stone_body_bot':(37,44,71,47,-4.5,'stone tower, base width (battered walls)'),
 'banner_C':(47.5,21.5,56.5,37,-4.6,'red demon banner, horned skull, frayed gold-fringed tip'),
 'arch_outer':(40,32.5,62,47,-4.9,'stone arch frame, 9 voussoirs, block jambs'),
 'arch_opening':(44.5,34,58.5,46,-4.9,'dark red interior'),
 'horned_skull':(45.5,29.5,56.5,35,-5.0,'bone skull with two curved horns over the keystone'),
 'crystal':(47.5,35.5,54,45.5,-4.0,'red emissive crystal cluster: 1 tall + 4 small shards'),
 'brazier_L':(38,39,42,47,-5.5,'timber post brazier, iron bowl, fire'),
 'brazier_R':(59.5,39,64,47,-5.5,'same'),
 'barrel_C':(63,41.5,68.5,47.5,-5.6,'orange barrel with red/gold emblem'),
 'steps':(48,47,56,53,-6.2,'5 stone steps down to water'),
 'rocks_C':(30,46,75,56,-6.5,'grey rock islet, moss, foam at base'),
 'flagpole':(42.5,2.5,43.5,18,-3.6,'pole with red flag'),
 'flag':(36,3,47,11,-3.6,'red flag with gold sigil, waving left'),
 'chain_mast':(38.5,6,40,30,-4.0,'mast with cross spars, chains to side towers'),
 # left archer tower
 'L_top_deck':(15.5,24.5,34,27,-4.2,'top platform beam'),
 'L_top_rail':(15.5,22,34,24.5,-4.2,'railing, 5 posts'),
 'L_banner':(21.5,27,27.5,34.5,-4.2,'red skull banner'),
 'L_low_deck':(7,37,35,39.5,-5.2,'lower deck beam'),
 'L_low_rail':(7,35,35,37,-5.2,'railing'),
 'L_legs':(13,27,33,50,-4.6,'4 square legs + X braces'),
 'L_ladder':(17,39,21,52,-5.6,'ladder, 9 rungs, leaning'),
 'L_fire1':(9,34,14,37,-5.2,'fire pot'),
 'L_fire2':(25,35,31,38,-5.2,'fire pot (big)'),
 'L_archer':(22,15,31,24,-3.8,'hooded horned archer, drawing bow'),
 'rocks_L':(0,43,30,55,-6.0,'rock islet under left tower'),
 # right archer tower
 'R_top_deck':(69,26,86,29.5,-4.0,'top platform beam'),
 'R_top_rail':(69,23.5,86,26,-4.0,'railing'),
 'R_low_deck':(76,40,97,42,-5.4,'lower deck'),
 'R_low_rail':(76,37.5,97,40,-5.4,'railing'),
 'R_legs':(71,29,95,51,-4.6,'legs + X braces'),
 'R_scaffold':(63,32,71,44,-4.8,'inner scaffold between tower and right deck'),
 'R_cannon':(82,33.5,92,40,-5.2,'black iron cannon on carriage, muzzle toward camera-right'),
 'R_archer':(74,17,87,27,-3.8,'archer'),
 'rocks_R':(75,45,100,53,-5.6,'rock islet'),
}
FG_DECK = {  # foreground props: (u,v) of the point touching the deck plane y=0, plus screen bbox
 'post_L':((21,80),(17,63,25,80)),'post_R':((79,80),(75,63,83,80)),
 'lantern':((6,69),(0.5,55,12,69)),'barrel_L':((5.5,84),(0,70,11,84)),'ball_crate':((19,84),(11,74,27,84)),
 'rope_barrel_R':((95,84),(89,67,100,84)),'corner_banner_L':((4,100),(0,87,8,100)),'corner_banner_R':((96,100),(92,100,100,100)),
}
def ground_hit(u,v,y=0.0):
    d=ray(u,v); t=(y-CAM[1])/d[1]; return tuple(round(CAM[i]+t*d[i],2) for i in range(3))
if __name__=='__main__':
    for k,(u0,v0,u1,v1,X,n) in E.items():
        a=on_plane(u0,v1,X); b=on_plane(u1,v0,X)
        print(f"| {k} | ({u0},{v0})-({u1},{v1}) | x={X} | y {a[1]}..{b[1]} | z {b[2]}..{a[2]} | w {round(a[2]-b[2],2)} h {round(b[1]-a[1],2)} | {n} |")
    for k,((u,v),bb) in FG_DECK.items():
        g=ground_hit(u,v); print(f"| {k} | {bb} | deck point x={g[0]} z={g[2]} |")
