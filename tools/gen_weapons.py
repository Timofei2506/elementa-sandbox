import json
OUT="/home/user/elementa-sandbox/Assets/Resources/ElementaData"
# id, ru, cat, kind, mass g, velocity m/s, caliber, rpm, accuracy, recoil, auto, mag
W=[
("pistol_9mm","Пистолет 9мм","pistol","bullet",7.5,380,9.0,180,0.88,0.7,False,17),
("pistol_45","Пистолет .45 ACP","pistol","bullet",14.9,260,11.4,170,0.86,1.0,False,8),
("revolver_357","Револьвер .357","pistol","bullet",10.2,420,9.1,120,0.9,1.3,False,6),
("smg_9mm","ПП 9мм","smg","bullet",7.5,400,9.0,800,0.82,0.6,True,32),
("rifle_556","Автомат 5.56","rifle","bullet",4.0,940,5.56,750,0.93,0.9,True,30),
("rifle_762","Автомат 7.62","rifle","bullet",8.0,730,7.62,600,0.9,1.4,True,30),
("ak_762","АК 7.62x39","rifle","bullet",7.9,715,7.62,600,0.86,1.6,True,30),
("sniper_338","Снайперка .338 LM","sniper","ap",16.2,880,8.6,40,0.98,3.0,False,5),
("sniper_50","Антиматериальная .50 BMG","sniper","ap",42.0,860,12.7,30,0.97,4.5,False,5),
("shotgun_buck","Дробовик картечь","shotgun","frag",3.5,400,8.4,90,0.7,2.5,False,8),
("shotgun_slug","Дробовик пуля","shotgun","bullet",28.0,430,17.5,90,0.85,2.8,False,8),
("ap_round","Бронебойный 12.7","rifle","ap",42.0,900,12.7,300,0.95,3.5,True,10),
("incendiary_556","Зажигательный 5.56","rifle","incendiary",3.8,960,5.56,750,0.9,0.9,True,30),
("grenade_frag","Граната осколочная","explosive","explosive",180,25,60,1,0.9,0,False,1),
("rpg","РПГ (кумулятивная)","launcher","shaped",580,300,85,1,0.97,5,False,1),
("c4","C4 заряд","explosive","explosive",400,1,100,1,1.0,0,False,1),
("arrow_bow","Лук стрелы","bow","arrow",28,80,8,20,0.75,0.2,False,1),
("crossbow","Арбалет","bow","arrow",40,120,10,1,0.9,0.3,False,1),
("laser_rifle","Лазерная винтовка","energy","laser",0.001,299792458,2,60,0.99,0,False,20),
("taser","Электрошокер","energy","bullet",2,90,6,1,0.85,0,False,1),
("flamethrower","Огнемёт","flame","incendiary",20,60,20,600,0.6,0.5,True,100),
("harpoon","Гарпун","bow","arrow",900,45,20,1,0.8,1.0,False,1),
("railgun","Рельсотрон","energy","ap",20,2500,8,10,0.99,6,False,1),
]
weapons=[dict(id=i,ru=r,cat=c,kind=k,mass=m,velocity=v,caliber=cal,rpm=rp,accuracy=ac,recoil=rc,auto=au,mag=mg) for (i,r,c,k,m,v,cal,rp,ac,rc,au,mg) in W]
json.dump({"weapons":weapons}, open(f"{OUT}/weapons.json","w"), ensure_ascii=False, indent=1)
print("weapons:",len(weapons))
