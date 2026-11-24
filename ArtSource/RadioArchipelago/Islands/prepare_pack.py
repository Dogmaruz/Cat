import hashlib,json,math,pathlib,shutil

root=pathlib.Path(__file__).resolve().parent
pack=root/'ReferencePack_v1';guides=root/'RouteGuides_v1'
source=root/'Source';source.mkdir(parents=True,exist_ok=True)
original=pathlib.Path('C:/Users/Derek/OneDrive/Desktop/exec-b67b94fb-ebab-4449-b255-75b7959d0f04.png')
shutil.copyfile(original,source/'design-reference.png')
style=pathlib.Path('C:/Users/Derek/.codex/generated_images/01a15adc-b8d7-77b0-be33-be2b0b5f56d4/exec-c6ec1b4d-fb35-496a-977a-ae26e29a1ab9.png')
shutil.copyfile(style,source/'radio-harbor-style-anchor.png')

specs=[
 ('01_radio_harbor','Гавань эфира','A broad oval floating limestone plateau with a small round ivory radio house, domed roof, bronze-rimmed round front speaker window and a mint arched door. Exactly two cypress trees WEST of the house, golden shrubs on the EAST, one coral roof pennant. Main house in the northern inner portion; path passes south in front of it and sweeps broadly around the trees.','rounded oval',.90,.76,290,-130,.045,0),
 ('02_signal_lighthouse','Маяк дальнего сигнала','A tall elegant ivory lighthouse with three restrained brass collars and a mint glass lantern, one low mint-roof keeper house EAST, exactly one tall cypress WEST and a coral windsock. Broad crescent terrace and a weathered coastal rock arch on the outer eastern edge. The paved route runs in a generous sweeping arc past the keeper house and around the lighthouse with open sight lines.','broad crescent',.91,.73,300,-115,.035,.7),
 ('03_bell_gardens','Сады колокольных арок','A terraced hanging garden with exactly three connected ivory stone arches around a sunken circular herb garden; one bronze bell under the southern arch; dark cypress pair in the northwest; golden flowers and low curved walls. Broad stone road curves past the arches, then around a raised garden knoll, with ample grass shoulders.','softly lobed garden',.88,.78,280,-135,.05,1.1),
 ('04_wind_workshop','Ветряная мастерская','A friendly ivory and muted terracotta workshop with a short brass wind mast carrying exactly three cream-and-coral cloth sails; low timber work veranda on the east, small mint copper water tank on the northwest, one cypress near the veranda. A broad sweeping paved road bends around the workshop and a rounded limestone outcrop; no tight corners.','kidney-shaped terraces',.93,.72,305,-140,.055,.2),
 ('05_star_observatory','Звёздная обсерватория','An ivory observatory with a shallow mint copper dome and an elegant brass armillary sphere of three rings on the rear tower; two cypress trees on the east and one low stone star-gazing terrace on the west. A spacious pale stone road makes a long smooth arc around the observatory and the low terrace.','rounded observatory mesa',.90,.79,295,-130,.04,1.5),
 ('06_crystal_lagoon','Хрустальная лагуна','A shallow contained mint turquoise lagoon inside a limestone island, exactly three broad translucent turquoise crystal clusters around its northern bank, one low ivory footbridge over its eastern outlet and two narrow waterfalls descending the outer rock edge. Broad dry stone path skirts the lagoon smoothly and crosses the gently curved bridge; the road never vanishes under water.','lagoon bowl',.90,.77,300,-125,.035,2.1),
 ('07_sailboat_port','Порт парусных писем','A miniature stone sky-port with two brass mooring masts, cream-and-coral triangular sail awnings, a low ivory glider hangar on the north side and one tiny empty wooden sail skiff moored EAST. One cypress WEST, brass radio beacon near the hangar. A wide stone quay road curves gently past the hangar and around the mooring lawn, no square dock corners in the driving line.','elongated harbor shelf',.96,.69,285,-135,.04,.5),
 ('08_sun_greenhouse','Тёплая оранжерея','A refined compact greenhouse of mint-tinted solid glass panels and copper ribs on an ivory plinth, with golden flowering plants inside; a small cream potting shed to its west, three low planters and one cypress on the east. A generous curving stone road winds around the greenhouse and a soft grass mound. Use readable large glass panels rather than filigree.','gentle garden terraces',.91,.77,295,-120,.045,2.7),
 ('09_echo_canyon','Каньон резонанса','A connected twin-height floating limestone mesa with a deep central cleft, one broad gently arched stone bridge across the northern part of the cleft, a solid natural front land bridge, and three large bronze resonance pipes on the northeastern plateau. A cypress pair and golden grass on the west. The broad continuous road skirts the cleft and crosses the northern bridge on a gradual arc with no right-angle approaches.','two-lobed canyon mesa',.94,.74,310,-135,.04,1.9),
 ('10_broadcast_citadel','Башня большого эфира','A grand but friendly ivory radio citadel: central round tower, two smaller unequal towers, large elegant brass signal ring above the central dome, arched mint shutters, one coral pennant and three cypress trees. Broad two-tier limestone terraces with a sweeping pale road around the outer terraces; gradual curved ramps replace square stair turns. Make this the distinctive destination island without religious symbols or lettering.','broad citadel crown',.92,.80,300,-130,.035,3.0),
]
manifest=[]
for ident,title,description,shape,rx,rz,sweep,start,a,phase in specs:
    folder=pack/ident;folder.mkdir(parents=True,exist_ok=True)
    guide=guides/ident;guide.mkdir(parents=True,exist_ok=True)
    def point(t):
        theta=math.radians(start)+math.tau*t
        modulation=1+a*math.sin(t*math.tau+phase)+.015*math.sin(2*t*math.tau)
        return (rx*.88*modulation*math.cos(theta),rz*modulation*math.sin(theta))
    loop=[point(i/600) for i in range(601)]
    loop[-1]=loop[0]
    passes=2.5
    points=[point(passes*i/1500) for i in range(1501)]
    loop_length=sum(math.dist(aa,bb) for aa,bb in zip(loop,loop[1:]))
    length=loop_length*passes
    duration=75;speed=6.5;scale=duration*speed/length
    radii=[]
    for aa,bb,cc in zip(points,points[1:],points[2:]):
        la=math.dist(aa,bb);lb=math.dist(bb,cc);lc=math.dist(aa,cc)
        cross=abs((bb[0]-aa[0])*(cc[1]-aa[1])-(bb[1]-aa[1])*(cc[0]-aa[0]))
        if cross>1e-12:radii.append(la*lb*lc/(2*cross)*scale)
    route=dict(schemaVersion=2,referenceOnly=True,durationTargetSeconds=duration,durationRangeSeconds=[60,90],forwardSpeedExample=speed,
               arcLengthTarget=duration*speed,normalizedLength=length,uniformXZScaleExample=scale,minRadiusAtExampleScale=min(radii),
               physicalLoopClosed=True,examplePasses=passes,physicalLoopNormalizedLength=loop_length,exampleSecondsPerLoop=duration/passes,
               coordinateConvention='x=east/right, z=north/back. TOP: north up. FRONT camera south; RIGHT camera east; LEFT camera west; BACK camera north.',
               description='One closed smooth physical road reused over time. Example song travels 2.5 passes, not 2.5 parallel roads. Tiles must be activated by song time/progress, never all laps at once. These samples are a conceptual route to refit to final 3D terrain. Generator timing is unchanged. Not a verified collider or production spline; use arc-length resampling in implementation.',
               analyticParameters=dict(startAngleDegrees=start,sweepDegrees=360*passes,radiusX=rx*.88,radiusZ=rz,
                   radialAmplitude=a,radialPhase=phase,secondaryAmplitude=.015),
               maxHeadingSpeedDegreesPerSecondExample=math.degrees(speed/min(radii)),
               minimumApproximate180TurnSeconds=math.pi*min(radii)/speed,
               physicalLoopPoints=[dict(x=x,y=0,z=z) for x,z in loop],
               samplePoints=[dict(x=x,y=0,z=z) for x,z in points],roadWidthNormalized=.12)
    (folder/'route.json').write_text(json.dumps(route,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    tosvg=lambda q:(512+q[0]*400,512-q[1]*400)
    xy=[tosvg(q)for q in loop]
    path='M '+' L '.join(f'{x:.2f},{y:.2f}'for x,y in xy)+' Z'
    sx,sy=tosvg(points[0]);ex,ey=tosvg(points[-1])
    svg=f'''<svg xmlns="http://www.w3.org/2000/svg" width="1024" height="1024" viewBox="0 0 1024 1024"><rect width="1024" height="1024" fill="#f3f4ef"/><ellipse cx="512" cy="512" rx="405" ry="370" fill="#d3dec8"/><ellipse cx="512" cy="470" rx="115" ry="105" fill="#d9c9b0"/><path d="{path}" fill="none" stroke="#b6ba9e" stroke-width="68" stroke-linejoin="round"/><path d="{path}" fill="none" stroke="#eee2cb" stroke-width="48" stroke-linejoin="round"/><circle cx="{sx:.2f}" cy="{sy:.2f}" r="14" fill="#55c5ab"/><circle cx="{ex:.2f}" cy="{ey:.2f}" r="14" fill="#f77c64"/></svg>'''
    (guide/'route-guide.svg').write_text(svg,encoding='utf8')
    spec=dict(id=ident,title=title,description=description,rockShape=shape,views=['front','top','right','left','back'],
              styleReference=str(source/'radio-harbor-style-anchor.png'),designReference=str(source/'design-reference.png'),
              routeGuide=str(guide/'route-guide.png'),routePlan=str(folder/'route.json'))
    (folder/'brief.json').write_text(json.dumps(spec,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    manifest.append(spec)
(root/'pack-specs.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print('Prepared 10 island briefs, 50 separate view targets and 10 smooth closed-loop route references (2.5 passes / 75 seconds).')
