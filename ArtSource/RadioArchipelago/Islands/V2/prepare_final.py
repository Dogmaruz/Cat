"""Final v2 briefs: diverse biomes, winding repeatable circuit, courier architecture."""
import json,pathlib
ROOT=pathlib.Path(__file__).resolve().parent
worlds=json.loads((ROOT/'worlds.json').read_text(encoding='utf8'))
architecture={
 '01_emerald_ravine':'Two off-center mountain radio homes integrated into the NORTHWEST slate ridge, dark moss-green pitched roofs, rounded copper speaker vents and thick short antenna forks. One low timber/bronze pump workshop on the SOUTHEAST brook bank, clear of bridge and road. A few large readable buildings, not tiny toy clutter. Clear some trees for their pads while keeping the forest world.',
 '02_coral_oasis':'A group of two stepped coral-adobe radio homes EAST of the oasis, flat red roofs with thick brass vent stacks and short aerials; a squat eroded sandstone signal/watch tower on the WEST butte with a copper ring receiver. Rounded doors/speaker vents and brass mechanisms connect to the courier world. Desert materials, no ivory village.',
 '03_amethyst_grotto':'One low indigo-stone radio research workshop on the NORTHWEST mineral ledge with lavender metal roof and chunky silver/brass vent; two squat crystal-cut courier homes on the SOUTHEAST shelf, short thick antenna forks and circular speaker grilles. Facades visible in OPEN ravine, never long enclosed cave architecture.',
 '04_autumn_windmills':'Keep the big off-center timber windmill with broad cloth blades and brass hub. Add a russet timber courier home on the WEST hill and a low red-roof radio workshop on the SOUTHEAST shoulder with copper vent stacks and short antenna. Ochre stone plinths and warm wood, no cream domes. All pads outside the road.',
 '05_frost_moon':'Two squat radio homes embedded into NORTHWEST ice ridge above the lake; one moon-silver workshop on SOUTHEAST lobe beyond the lower bend. Frosted blue metal roofs, cobalt doors, warm brass round vents and thick roof antenna forks. Retain low crescent beacon on NORTHEAST with chunky geared base. No tall central tower.',
 '06_tropical_cascade':'Upgrade EASTERN thatched hut into radio depot with bamboo/ochre walls, brass vents and one chunky dish. Add two palm-roof mountain courier homes into NORTHWEST terrace and an open-sided timber pump workshop on SOUTHWEST shelf. A short wooden windmill with brass hub on eastern rear shelf if room. Keep all waterfall and bridge clearance open.',
 '07_storm_coast':'Two low weatherproof radio homes embedded in NORTHWEST basalt headland; NORTHEAST shelter deck becomes a timber/navy-steel radio depot with chunky roof antenna and heavy brass exhaust. One squat rust-red signal/windmill tower on SOUTHEAST rock buttress, thick paddle arms instead of a fine lattice mast. Damp timber, navy roofs and rusty brass.',
 '08_sunflower_terraces':'Keep SOUTHEAST red barn, make it a courier/radio workshop with one chunky brass vent and short antenna. Add a low red-timber radio home on NORTHWEST wheat terrace and a squat wooden windmill with brass hub on outer WEST/SOUTHWEST field shoulder. Fountain/pond and sunflower masses remain. Three readable silhouettes, not a central tower.',
 '09_volcanic_garden':'Basalt/brick-red courier workshop on NORTHEAST high bluff with broad brass chimney and short antenna; one low basalt radio home on WEST rust slab; squat copper turbine/relay on SOUTHEAST ridge outside stone arch. Warm red roofs/copper mechanisms contrast charcoal rock. Sealed ember pool and sculptural red tree remain anchors.',
 '10_cloud_observatory':'Preserve SOUTHEAST open low gold observatory pavilion. Add NORTHWEST peach-ceramic radio workshop with lavender roof, chunky brass vents and short aerial; SOUTHWEST mauve courier home; low periwinkle relay plinth on NORTHEAST ledge with one gold dish. Central cloudwell and short lavender stone arch remain. Low legible buildings, no generic castle.'
}
RULES='''Physical route MUST follow the NEW closed guide: a long SERPENTINE CIRCUIT passing through the INTERIOR with many alternating smooth bends, then a return corridor JOINING the start tangentially. One continuous repeatable road, NO DEAD ENDS and NO ordinary round perimeter racetrack. Keep the winding interior parts dominant. No same-level intersections, shortcuts, disconnected segments or sharp junction. Reserve generous clear shoulders on both sides for later musical tiles. No labels, marker dots, arrows, compass, UI, characters or glowing tile chain.
TRIPO GEOMETRY: OPEN RAVINES and visible cuttings. NO enclosed caves or long tunnels, no dark tunnel mouth. Only a very SHORT thick natural stone ARCH like a small stone bridge, open to daylight on BOTH sides, with clearly visible road continuing through; roof length no more than road width. Broad flat bridge decks with smooth bank connections. No stairs, house entrances, machines or vegetation across the road.
Add the specified courier-world buildings OUTSIDE road shoulders on broad raised pads. Rounded functional doors, chunky brass/copper radio vents, short sturdy antennas and visible simple mechanisms match the courier characters. Buildings use each world's OWN materials/palette; do not recolor all worlds cream/mint. Keep main terrain silhouette, biome landmarks and distinct colors from draft. Avoid huge central tower surrounded by a circle. Few large readable structures and coherent landscape, not tiny crowded scatter.
'''
for w in worlds:
    w=dict(w);w['architecture']=architecture[w['id']]
    w['landmarks']=w['landmarks'].replace('one thick irregular cave roof over a SHORT visible road passage','one very short thick stone bridge-like arch with daylight on both sides').replace('open-cut grotto','open ravine').replace('No tower or central house.','').replace('No central tower.','')
    w['terrain']=w['terrain'].replace('winding OPEN cross-island road','winding cross-island circuit').replace('faceted cavern island','faceted open-ravine island').replace('open-cut grotto','open-cut mineral ravine')
    w['journey']=w['journey']+' Then follow the NEW guide return corridor back into the beginning smoothly and continue the same winding circuit as many times as needed.'
    folder=ROOT/'FinalReferences'/w['id'];folder.mkdir(parents=True,exist_ok=True)
    identity='\nWORLD: '+w['title']+'\nPALETTE: '+w['palette']+'\nTERRAIN: '+w['terrain']+'\nLANDSCAPE: '+w['landmarks']+'\nCOURIER ARCHITECTURE: '+w['architecture']+'\nROAD MATERIAL: '+w['road']+'\nART DIRECTION: '+w['style']
    top='''STRICT VERTICAL TOP-DOWN ORTHOGRAPHIC modeling SITE PLAN. Camera at zenith looking straight DOWN, no perspective tilt, no cliff sides or vertical facades. ONE high-quality stylized 3D terrain image on PALE NEUTRAL GREY background with whole footprint and12%margin. Image1 is the NEW CLOSED technical road plan: it OVERRIDES the old road in image2. Image2 is the SAME WORLD draft, preserve its biome, land shape, quality and material character while replacing its open road with the NEW winding closed route and adding the specified buildings. Tree crowns/roofs seen from above, no trunks or cliffs. Ignore schematic symbols/colors/dots. Render beautiful readable terrain materials and architecture, not flat diagram.
'''+RULES+identity+'\nExactly ONE top view; no collage or extra panels.'
    common='''Use case: stylized-concept. ONE complete 3D environment modeling asset view, premium crafted stylized materials, same biome world. Whole island and underside/trees/building tips/waterfall bottoms visible with12%margin, neutral pale-grey studio background, no horizon/ocean/fog hiding geometry.
'''+RULES+identity
    cameras={
      'front':'SOUTH FRONT elevation looking NORTH. The final TOP defines north-up/east-right positions; south is near camera. Moderate front elevation with vertical cliff faces and full underside, not another overhead image.',
      'right':'EAST RIGHT side looking WEST. SOUTH is IMAGE RIGHT; NORTH IMAGE LEFT. Actual eastern faces in foreground. Southern front entrances are edge-on on image right; western buildings behind eastern terrain. Full quarter-turn, not another south-front view.',
      'left':'WEST LEFT side looking EAST. SOUTH is IMAGE LEFT; NORTH IMAGE RIGHT. Western buildings/terrain foreground, east features behind. Opposite of right view, not a mirrored front.',
      'back':'NORTH BACK side looking SOUTH. WEST is IMAGE RIGHT; EAST IMAGE LEFT. Show rear walls with front doors hidden, northern landmarks foreground. Full180-degree reversal from front, no duplicated front facades.'}
    new={'top':top}
    edits={}
    for view,camera in cameras.items():
        new[view]='CAMERA FIRST: '+camera+'\nImage1 FINAL TOP fixes the exact NEW winding closed route and new building positions. Image2, when supplied, is the FINAL FRONT identity/underside reference. Rotate camera around the SAME fixed island; keep architecture and route inventory fixed.\n'+common
        if view=='front':roles='Image1 is the existing FRONT draft: preserve its landmass, camera orientation, materials and world identity. Image2 is the NEW FINAL TOP: update the road to its CLOSED winding itinerary and add the SAME new tech buildings seen there. Replace old open road completely. Do not simply copy the unchanged draft.'
        else:roles='Image1 is the existing '+view.upper()+' draft: preserve this side camera orientation and landmass. Image2 FINAL TOP fixes NEW CLOSED winding route/building positions; image3 FINAL FRONT fixes the new building identities and materials. Apply these SAME additions in the correct projection/occlusion. Do not simply copy unchanged draft or invent another island.'
        edits[view]='CAMERA FIRST: '+camera+'\n'+roles+'\n'+common
    (folder/'brief.json').write_text(json.dumps(w,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    (folder/'prompts.json').write_text(json.dumps(new,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    (folder/'edit-prompts.json').write_text(json.dumps(edits,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    w['referenceFolder']=str(folder)
    w['routeGuide']=str(ROOT/'RouteGuidesClosed'/w['id']/'route-guide.png')
    w['routePlan']=str(ROOT/'RouteGuidesClosed'/w['id']/'route.json')
    w['generationOrder']=['top','front','right','left','back']
    w['draftFolder']=str(ROOT/'ReferencePack'/w['id'])
    if 'final' not in globals():final=[]
    final.append(w)
(ROOT/'worlds-final.json').write_text(json.dumps(final,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print('Prepared final closed-route + courier-architecture briefs/prompts for50 separate views. Drafts preserved.')
