"""Creative briefs for the revised non-circular, diverse island worlds."""
import json,pathlib,shutil
ROOT=pathlib.Path(__file__).resolve().parent
ROOT.joinpath('Source').mkdir(parents=True,exist_ok=True)
shutil.copyfile('C:/Users/Derek/OneDrive/Изображения/Снимки экрана/Снимок экрана 2026-10-26 225522.png',ROOT/'Source/user-route-sketch.png')
worlds=[
 dict(id='01_emerald_ravine',title='Изумрудное ущелье',palette='Deep emerald and moss green, cool weathered grey slate, tiny pale blue stream, warm chestnut timber. No ivory architecture or gold flower carpet.',
      terrain='A long, irregular cleft landmass, broad forest glades separated by a shallow winding slate ravine. One connected substantial floating terrain slab with dark rooted rock underside, very asymmetric rather than a disk.',
      landmarks='An off-center thick fallen-tree bridge over the brook; a low wide slate natural arch forming a short open-ended cave; big rounded tree crowns gathered in two groves; fern glade with moss boulders. No tower or central house.',
      road='Wide worn grey flagstone trail with soft earth shoulders, thick timber surfacing only on the bridge.',
      journey='Weave through a grove, curve inward across the brook, travel through the short slate arch, emerge into a central glade, then bend outward between boulders. The island has room on both sides of the road.',
      style='Rounded expressive forest shapes with textured moss and broad painterly leaf masses.'),
 dict(id='02_coral_oasis',title='Коралловый оазис',palette='Coral pink and terracotta sandstone, apricot sand, vivid turquoise water, dusty teal palms and faded red adobe. No white stone castle.',
      terrain='Broad stepped red-sandstone mesas joined by thick low natural saddles, an asymmetrical many-sided desert landmass with flat tall buttes and a bowl-like oasis depression.',
      landmarks='A small off-center flat-roof adobe settlement, one turquoise pond with chunky palms, a thick eroded sandstone gate and sculptural wind-carved rock fins. Keep generous areas of open sand.',
      road='Broad compacted rose-sand road with embedded rust-colored slabs, low dune shoulders, no continuous circular curb.',
      journey='Thread between sandstone fins, bend through an adobe gate into the interior, sweep beside the pond then reverse the bend toward a broad saddle and the opposite edge.',
      style='Chunky sun-baked terracotta forms and stylized layered erosion, large uncluttered warm surfaces.'),
 dict(id='03_amethyst_grotto',title='Аметистовый грот',palette='Violet and midnight indigo rock, luminous lavender amethyst, restrained cyan mineral pools, silver-blue fungi. No green garden or cream building.',
      terrain='A lopsided faceted cavern island with one large open-cut grotto on one side and a lower mineral shelf extending far to the other. Thick connected purple rock mass, not a round platform.',
      landmarks='Three substantial amethyst crystal clusters, one thick irregular cave roof over a SHORT visible road passage, a shallow cyan mineral pool, chunky silver-blue mushrooms. No central tower.',
      road='Broad muted plum stone paving with a pale lavender mineral seam along one edge, rock shoulders on both sides.',
      journey='Bend down into the open-cut grotto, pass below its short thick arch, emerge along a mineral pool, change curve direction between crystal outcrops, then cross an interior saddle.',
      style='Stylized faceted minerals and smooth sculpted cave walls with controlled soft gemstone glow.'),
 dict(id='04_autumn_windmills',title='Осенние ветряные холмы',palette='Burnt orange, russet red, ochre grass, dark timber, muted blue brook and smoke-grey rock. Autumn woodland, not mint/ivory.',
      terrain='Rolling multi-lobed hills with broad grassy saddles, exposed rust-brown shale underside, low rounded rises spread across an irregular elongated footprint.',
      landmarks='One off-center timber windmill with four thick broad cloth-covered blades, a small timber footbridge, autumn maples with orange/red crowns, a modest pumpkin patch and a brook. No domed radio house.',
      road='Wide warm brown packed-earth lane, occasional timber planks and worn ochre cobbles, open grass shoulders.',
      journey='Wander between two autumn groves, cross the brook, sweep past the windmill on a generous local arc, cut back through a shallow central valley, then head across an open hill.',
      style='Warm storybook countryside, broad painted tree crowns and tactile timber carpentry.'),
 dict(id='05_frost_moon',title='Лунный ледник',palette='Glacier blue, pale cyan ice, white snow, cold cobalt cracks and moon-silver metal. Very sparse violet highlights. No beige limestone or coral flags.',
      terrain='An angular split ice shelf with two broad staggered levels joined by a smooth wide snow ramp, faceted blue frozen underside and a large diagonal fissure.',
      landmarks='One thick translucent-blue ice arch, an off-center low silver crescent beacon, three chunky frost boulders and a small still frozen lake. Sparse winter shrubs, no cypresses.',
      road='A broad blue-grey gritted snow trail clearly contrasting with white snow; smooth gentle ramps without stairs, no full-loop edging.',
      journey='Sweep around a frost boulder, turn inward across the lower shelf, pass under the thick ice arch, curve along the frozen lake, then gradually climb the broad ramp and change heading again.',
      style='Clean crystalline winter shapes, soft snow volume and readable thick ice forms.'),
 dict(id='06_tropical_cascade',title='Тропические каскады',palette='Bright jade jungle, saturated turquoise water, dark warm volcanic tuff, orange hibiscus accents and reddish palm trunks. No cream village.',
      terrain='Tall staggered tropical terraces arranged diagonally on a deep irregular island mass, broad vegetated ramps joining shelves, two opaque water ribbons descending as finite tiered waterfalls.',
      landmarks='Two tiered waterfalls, one thick supported stone bridge over a channel, an off-center low thatched hut, chunky palm crowns and a quiet fern clearing. Keep the road dry and unobscured.',
      road='Wide dark basalt pavers through jade grass, ochre timber bridge deck, low fern shoulders.',
      journey='Thread through palms, bend inward behind a short waterfall arch with obvious rock clearance, cross the broad supported bridge, sweep through the fern clearing, then curve down a gentle terrace ramp.',
      style='Lush broad tropical forms, saturated foliage and sculptural water ribbons with clear solid edges.'),
 dict(id='07_storm_coast',title='Грозовой берег',palette='Navy and storm grey, nearly black basalt, blue-green inlet water, weathered brown wood and rusty red nautical accents. No warm ivory garden.',
      terrain='A low long jagged coastline slab with a diagonal sheltered inlet cutting deeply into the interior, uneven black basalt headlands connected by broad inland land corridors.',
      landmarks='A short off-center rust-red semaphore mast with thick paddles, low timber rain shelter, sheltered inlet and heavy basalt arch. One small pond with rain ripple relief; no airborne rain streaks or mist hiding geometry.',
      road='Wide rough slate coastal road changing to thick brown timber decking near the shelter, grey gravel shoulders.',
      journey='Turn inland between basalt headlands, skirt the deep inlet on alternating broad curves, pass through the heavy arch, cross an exposed grassy interior and arrive beside the shelter.',
      style='Bold nautical silhouettes, simplified weathered basalt and damp material sheen, cool overcast material lighting.'),
 dict(id='08_sunflower_terraces',title='Солнечные поля',palette='Golden yellow fields, fresh light green grass, barn red, rich brown soil, muted sky-blue fountain water and warm tan earth. No dark cypresses or white domes.',
      terrain='A wide asymmetric farm plateau with shallow diagonal agricultural terraces and rounded loess cliffs with thick grass-root edges. Open fields occupy much more area than buildings.',
      landmarks='One small off-center red timber barn, two chunky sunflower field patches, a low broad stone fountain, a shallow duckless pond and a few apple trees. Large simple crop masses rather than thousands of tiny flowers.',
      road='Broad ochre farm lane with gentle wheel-worn relief and short brick-paved courtyard segment; no stairs or continuous circular curb.',
      journey='Wind between sunflower patches, sweep through the inner fountain courtyard, curve beside the pond, cut across an open middle meadow, then bend toward the barn-side exit.',
      style='Sunny stylized farmland with broad friendly forms, matte painted timber and open readable terrain.'),
 dict(id='09_volcanic_garden',title='Вулканический сад',palette='Charcoal black basalt, deep brick-red mineral rock, ember orange cracks, red-leaf bonsai and sparse silver-green grass. Calm magical volcanic garden, not dangerous lava flooding the road.',
      terrain='An irregular horseshoe-shaped caldera mass with a broad solid inner saddle and a low open crater depression; chunky black columnar underside. The road CROSSES the interior saddle and changes direction; never simply follows the horseshoe rim.',
      landmarks='One off-center red-leaf sculptural tree, a shallow sealed ember mineral pool, thick basalt rock gate and two short opaque pale steam plumes tucked away from the road. No central tower.',
      road='Wide cool charcoal stone path with subtle rust-red aggregate and clear lighter ash shoulders, no glowing tile chain.',
      journey='Thread basalt columns, bend into the inner saddle, pass the red tree, turn through the broad crater floor, then curve under the thick rock gate and leave across a different ridge.',
      style='Strong sculpted basalt geometry with controlled emissive cracks and restrained red foliage.'),
 dict(id='10_cloud_observatory',title='Облачная обсерватория',palette='Peach and lavender cloudstone, pearly pink terraces, pale gold instruments, mauve shrubs and cool periwinkle inlays. Soft pastel celestial world, no green/mint Mediterranean garden.',
      terrain='Stacked broad rounded cloudstone ledges offset diagonally like a sculpted spiral hillside but with a winding OPEN cross-island road, connected by thick wide gently sloping saddles. No circular race track or concentric rings.',
      landmarks='An off-center OPEN low gold observatory pavilion with thick ribs, a shallow pearly cloudwell fountain, one lavender natural arch and three sculpted mauve shrubs. No tall castle or central domed tower.',
      road='Wide muted periwinkle stone-inlaid trail across peach terraces, gentle smooth ramps with no steps; ample pale shoulders.',
      journey='Bend between offset ledges, travel through the lavender arch into the interior, sweep beside the cloudwell, cross a broad saddle below the gold pavilion, then reverse curve toward a different edge.',
      style='Airy sculptural ceramic shapes and satin celestial metals, clear thick modelable structures.')
]
TOP='''Use case: stylized-concept. Asset: ONE standalone 3D environment modeling reference for Tripo.
STRICT VERTICAL TOP-DOWN ORTHOGRAPHIC SITE PLAN, camera at zenith looking straight DOWN. Image 1 is a schematic physical layout, not an illustration: transform its irregular land footprint and WINDING OPEN ROAD into beautiful detailed stylized terrain. Preserve the road's alternating left/right bends and INTERIOR CROSSINGS OF THE LAND, with two distinct endpoints. The path is NOT a ring or perimeter circuit. Do not add a circular road, concentric lanes or central round plaza. No same-level path junctions or tight hairpins. Terrain extends well beyond both road shoulders.
Show only top surfaces: roof apex centered over footprint, tree crowns, no vertical facades, no cliff sides or pointed underside visible. Ignore schematic colors, marker dots and guide marks. No labels, text, compass, arrows, characters, UI or glowing game tiles. The road is continuous broad physical terrain reserved for three later game lanes, not a stepping-stone chain. Smooth passable slopes and no stairs across travel. A short thick rock arch may hide only a tiny path segment, with clear entrance and exit.
'''
ELEVATION='''Use case: stylized-concept. Asset: ONE standalone environment modeling reference for Tripo.
Image 1 is the TOP layout of the SAME island: world NORTH up, EAST right. It fixes winding road geometry, terrain outline and landmark positions. Image 2, when present, gives the front identity/material/rock underside only. Change viewpoint, never redesign or mirror the island. Keep the same exact object inventory, architecture, colors and terrain proportions. No extra road circuit, central tower or duplicated landmarks.
The physical road has two ends and snakes LEFT/RIGHT THROUGH THE INTERIOR. Keep road flat/smooth with wide shoulders, no stair runs or sharp hairpins. Show the entire connected large landmass, all building/tree tips, cliffs, waterfall bottoms and rock underside with 12% framing margin. Neutral pale grey studio background, no ocean/horizon/clouds obscuring geometry. No text, labels, compass, arrows, UI, characters or glowing game tiles. Beautiful crafted stylized 3D materials and readable large forms; chunky separate objects suitable for modeling, no filigree, scattered floating debris or tiny clutter.
'''
camera={
 'front':'FRONT camera at SOUTH looking NORTH, centered with moderate elevation around25 degrees so the long interior road is readable. NORTH lies back. This is a modeling asset view, not a scene with horizon.',
 'right':'EAST/RIGHT ELEVATION: camera fully to the EAST looking WEST. SOUTH/front is IMAGE RIGHT; NORTH/back IMAGE LEFT. Show east terrain and eastern sides, not another front hero angle. Southern facades are edge-on to image right. Western landmarks move behind eastern landmarks.',
 'left':'WEST/LEFT ELEVATION: camera fully to the WEST looking EAST. SOUTH/front is IMAGE LEFT; NORTH/back IMAGE RIGHT. Show western sides and western terrain foreground. This is opposite to right view, not a mirror. Southern facade edge-on at image left.',
 'back':'NORTH/BACK ELEVATION: camera NORTH looking SOUTH, full180 degree reversal from front. WEST is IMAGE RIGHT; EAST IMAGE LEFT. Show actual rear walls, back of shelters and opposite terrain faces; front doors invisible. Do not duplicate southern entry onto the back.'}
for w in worlds:
    folder=ROOT/'ReferencePack'/w['id'];folder.mkdir(parents=True,exist_ok=True)
    identity='\nWORLD: '+w['title']+'\nPALETTE: '+w['palette']+'\nTERRAIN: '+w['terrain']+'\nLANDMARKS: '+w['landmarks']+'\nROAD MATERIAL: '+w['road']+'\nJOURNEY: '+w['journey']+'\nART DIRECTION: '+w['style']
    finish='\nOne complete island in ONE camera view, no collage, panels or embedded extra views. Neutral background; reserve margin around the full footprint. Render the image as final high-quality concept art, not as a flat schematic.'
    prompts={'top':TOP+identity+finish}
    for view,description in camera.items():prompts[view]=ELEVATION+identity+'\nCAMERA: '+description+finish
    (folder/'brief.json').write_text(json.dumps(w,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    (folder/'prompts.json').write_text(json.dumps(prompts,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
(ROOT/'worlds.json').write_text(json.dumps(worlds,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print('Prepared 10 diverse world briefs and 50 TOP-FIRST view prompts. No v1 or Unity files changed.')
