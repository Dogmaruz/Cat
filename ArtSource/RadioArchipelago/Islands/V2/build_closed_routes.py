"""NEW CLOSED revision; original open v2 guides remain untouched.

Periodic uniform cubic B-splines give C2 continuous repeatable lap geometry.
These are technical references, not final art or a fixed-duration song plan.
"""
from __future__ import annotations

import json
import math
import runpy
import sys
from pathlib import Path
from xml.sax.saxutils import escape

ROOT=Path(__file__).resolve().parent
base=runpy.run_path(str(ROOT/"build_routes.py"))
Image,ImageDraw=base["Image"],base["ImageDraw"]
dist=base["dist"]
inside=base["point_inside"]
segment_distance=base["point_segment_distance"]
resample=base["resample"]
turns=base["turns"]
intersections=base["intersections"]
shape_polygon=base["shape_polygon"]
draw_feature=base["draw_feature"]
svg_points=base["svg_points"]
PALETTES={w["id"]:w for w in base["WORLDS"]}
OUT=ROOT/"RouteGuidesClosed"
ROAD_WIDTH=30.0
SIZE=1400
VIEW_ORIGIN=-60
VIEW_SIZE=1120
BG="#192127"

# Additional controls continue from the old final point and return to its start.
# Different sides are chosen for distinct circuits; the interior core is retained.
RETURNS={
 "01_emerald_ravine":[(875,205),(960,290),(955,435),(890,555),(830,680),(730,775),(585,805),(430,825),(280,850),(175,805)],
 "02_coral_oasis":[(870,155),(800,85),(650,105),(500,145),(365,160),(220,120),(100,90),(30,180),(75,300)],
 "03_amethyst_grotto":[(850,445),(835,315),(745,210),(610,155),(465,155),(315,165),(205,230),(190,385),(185,535),(140,650)],
 "04_autumn_windmills":[(140,790),(40,715),(50,580),(70,450),(60,320),(110,220)],
 "05_frost_moon":[(840,300),(820,430),(875,550),(820,700),(705,785),(560,780),(430,830),(280,875)],
 "06_tropical_cascade":[(585,865),(455,850),(350,785),(275,690),(165,640),(110,535),(150,430),(100,320),(155,235)],
 "07_storm_coast":[(330,120),(210,195),(135,335),(115,475),(40,620),(50,765),(200,810),(305,710)],
 "08_sunflower_terraces":[(190,500),(120,570),(105,675)],
 "09_volcanic_garden":[(100,745),(150,835),(310,850),(460,820),(610,850),(760,800),(850,690),(935,560),(975,400),(940,250),(820,235),(775,300)],
 "10_cloud_observatory":[(555,875),(410,850),(290,790),(180,685),(145,540),(210,440),(160,340),(250,245),(335,190),(450,135),(570,125),(670,110),(735,130)],
}
RETURN_DESCRIPTIONS={
 "01_emerald_ravine":"Eastern woodland and southern low meadow return; double-S ravine core retained.",
 "02_coral_oasis":"Northern sandstone saddle return above the oasis sweep.",
 "03_amethyst_grotto":"Outer indigo rock valley through the north and west, outside the crystal gallery.",
 "04_autumn_windmills":"Short western orchard return; the hillside and lower meadow core stay central.",
 "05_frost_moon":"Eastern blue ice shelf and southern fractured plain return around the serpentine moon basin core.",
 "06_tropical_cascade":"Western lower jungle ascent returning to the upper canopy approach.",
 "07_storm_coast":"Western basalt valley return with a broad lower turn, away from the semaphore ridge.",
 "08_sunflower_terraces":"Compact western low-terrace return, preserving the diagonal ascent and upper fountain arc.",
 "09_volcanic_garden":"Southern ash terrace and eastern warm stone return beneath the inverted interior meander.",
 "10_cloud_observatory":"Cloud terrace return through the west and northern shoulder; deep interior C bends retained.",
}


def periodic_spline(controls,steps=36):
    """Uniform periodic cubic B-spline, globally C2, with exact periodic seam."""
    points=[]
    n=len(controls)
    for i in range(n):
        q=[controls[(i+k)%n] for k in (-1,0,1,2)]
        for j in range(steps):
            t=j/steps
            b=[(1-t)**3/6,(3*t**3-6*t*t+4)/6,(-3*t**3+3*t*t+3*t+1)/6,t**3/6]
            points.append(tuple(sum(b[k]*q[k][axis] for k in range(4)) for axis in (0,1)))
    points.append(points[0])
    return points


def seam_derivatives(controls):
    p0,p1,p2=controls[-1],controls[0],controls[1]
    return dict(position=[(p0[k]+4*p1[k]+p2[k])/6 for k in (0,1)],
                firstDerivative=[(p2[k]-p0[k])/2 for k in (0,1)],
                secondDerivative=[p0[k]-2*p1[k]+p2[k] for k in (0,1)])


def outline_clearance(route,outline):
    return min(min(segment_distance(p,a,b) for a,b in zip(outline,outline[1:])) for p in route)-ROAD_WIDTH/2


def expand_outline(outline,route):
    # Preserve all original land; add terrain locally only where the return needs
    # a shoulder. A raster union followed by a simplified outer contour avoids
    # enlarging the entire biome merely to fill one small shore cleft.
    mask=Image.new("L",(VIEW_SIZE,VIEW_SIZE),0)
    draw=ImageDraw.Draw(mask)
    xy=lambda p:(round(p[0]-VIEW_ORIGIN),round(p[1]-VIEW_ORIGIN))
    draw.polygon([xy(p) for p in outline],fill=255)
    draw.line([xy(p) for p in route],fill=255,width=114,joint="curve")
    pixels=mask.tobytes();width=VIEW_SIZE
    edges={}
    def edge(a,b):edges.setdefault(a,[]).append(b)
    for y in range(1,width-1):
        offset=y*width
        for x in range(1,width-1):
            k=offset+x
            if not pixels[k]:continue
            if not pixels[k-width]:edge((x,y),(x+1,y))
            if not pixels[k+1]:edge((x+1,y),(x+1,y+1))
            if not pixels[k+width]:edge((x+1,y+1),(x,y+1))
            if not pixels[k-1]:edge((x,y+1),(x,y))
    loops=[]
    while edges:
        start=next(iter(edges));p=start;loop=[p]
        for _ in range(20000):
            if p not in edges:break
            q=edges[p].pop()
            if not edges[p]:del edges[p]
            p=q;loop.append(p)
            if p==start:break
        if len(loop)>3 and loop[-1]==start:loops.append(loop)
    def area(poly):return abs(sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(poly,poly[1:])))/2
    boundary=max(loops,key=area)
    def simplify(points,epsilon=1.2):
        if len(points)<3:return points
        distances=[segment_distance(p,points[0],points[-1]) for p in points[1:-1]]
        if not distances or max(distances)<=epsilon:return [points[0],points[-1]]
        k=distances.index(max(distances))+1
        return simplify(points[:k+1],epsilon)[:-1]+simplify(points[k:],epsilon)
    middle=len(boundary)//2
    boundary=simplify(boundary[:middle+1])[:-1]+simplify(boundary[middle:])
    p=[(x+VIEW_ORIGIN,y+VIEW_ORIGIN) for x,y in boundary]
    addition=100*(area(p)/area(outline)-1)
    return p,addition


def nonlocal_separation(samples):
    """Minimum distance between samples >3 road-widths apart along the lap."""
    minimum=1e9
    total=samples[-1][2]
    points=samples[:-1:2]
    for i,a in enumerate(points):
        for b in points[i+1:]:
            along=abs(a[2]-b[2])
            along=min(along,total-along)
            if along>ROAD_WIDTH*3:
                minimum=min(minimum,dist(a,b))
    return minimum


def feature_clearances(features,route,outline):
    result=[]
    for f in features:
        polygon=shape_polygon(f)
        p=polygon+[polygon[0]]
        gap=min(min(segment_distance(q,a,b) for a,b in zip(p,p[1:])) for q in route[::2])-ROAD_WIDTH/2
        crosses=any(inside(q,p) for q in route[::2])
        on_land=all(inside(q,outline) for q in polygon)
        passage=f["kind"] in ("arch","bridge_banks","ravine")
        result.append(dict(name=f["name"],kind=f["kind"],roadEdgeClearance=round(gap,2),
                           intentionalShortPassage=passage,insideIsland=on_land,overlap=crosses or gap<0))
    return result


def building_pads(outline,route,features,count=3):
    candidates=[]
    for y in range(160,881,45):
        for x in range(140,901,45):
            p=(x,y)
            if not inside(p,outline):
                continue
            shore=min(segment_distance(p,a,b) for a,b in zip(outline,outline[1:]))
            road=min(dist(p,q) for q in route[::2])-ROAD_WIDTH/2
            landmark=min(dist(p,f["center"])-max(f["radii"]) for f in features)
            radius=min(47,shore-25,road-20,landmark-18)
            if radius>=31:
                candidates.append((min(road,shore,landmark),radius,p,road,shore,landmark))
    pads=[]
    for score,radius,p,road,shore,landmark in sorted(candidates,reverse=True):
        if any(dist(p,z["center"])<180 for z in pads):
            continue
        pads.append(dict(id=f"tech_pad_{len(pads)+1}",center=list(p),usableRadiusGuideUnits=round(radius,1),
                         minimumRoadEdgeClearanceGuideUnits=round(road-radius,1),minimumShoreClearanceGuideUnits=round(shore-radius,1),
                         use="Off-path technology building envelope; exact building architecture and placement may adapt."))
        if len(pads)==count:
            break
    assert len(pads)>=2,"Not enough broad off-path technology pads"
    return pads


def render(world,features,pads,outline,route,target):
    ss=2
    img=Image.new("RGB",(SIZE*ss,SIZE*ss),BG)
    draw=ImageDraw.Draw(img)
    scale=SIZE*ss/VIEW_SIZE
    xy=lambda p:(round((p[0]-VIEW_ORIGIN)*scale),round((p[1]-VIEW_ORIGIN)*scale))
    # Use a translated drawing proxy so shared landmark renderer retains identical units.
    class OffsetDraw:
        def polygon(self,points,**kwargs):
            shift=-VIEW_ORIGIN*scale
            return draw.polygon([(x+shift,y+shift) for x,y in points],**kwargs)
        def line(self,points,**kwargs):
            shift=-VIEW_ORIGIN*scale
            return draw.line([(x+shift,y+shift) for x,y in points],**kwargs)
        def ellipse(self,points,**kwargs):
            shift=-VIEW_ORIGIN*scale
            return draw.ellipse([(x+shift,y+shift) for x,y in points],**kwargs)
    svg=[f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{VIEW_ORIGIN} {VIEW_ORIGIN} {VIEW_SIZE} {VIEW_SIZE}" width="{SIZE}" height="{SIZE}">',
         f'<title>{escape(world["name"])} — closed repeatable interior route, reference only</title>',
         '<metadata>referenceOnly=true; periodic C2 closed road; no endpoint dots, start markers, arrows or labels; no fixed song duration.</metadata>',
         f'<rect x="{VIEW_ORIGIN}" y="{VIEW_ORIGIN}" width="{VIEW_SIZE}" height="{VIEW_SIZE}" fill="{BG}"/>']
    draw.polygon([xy(p) for p in outline],fill=world["terrain"])
    draw.line([xy(p) for p in outline],fill=world["rim"],width=round(12*scale),joint="curve")
    svg.append(f'<polygon points="{svg_points(outline)}" fill="{world["terrain"]}" stroke="{world["rim"]}" stroke-width="12" stroke-linejoin="round"/>')
    for f in features:
        draw_feature(OffsetDraw(),f,scale,svg)
    for pad in pads:
        x,y=pad["center"]
        r=pad["usableRadiusGuideUnits"]*.72
        p=[(x-r,y-r),(x+r,y-r),(x+r,y+r),(x-r,y+r),(x-r,y-r)]
        draw.polygon([xy(q) for q in p],fill="#647577")
        draw.line([xy(q) for q in p],fill="#a1adb0",width=round(2*scale),joint="curve")
        svg.append(f'<polygon points="{svg_points(p)}" fill="#647577" stroke="#a1adb0" stroke-width="2"/>')
    for width,color in ((ROAD_WIDTH+8,"#263036"),(ROAD_WIDTH+3,world["rim"]),(ROAD_WIDTH,world["road"])):
        cyclic=route[-4:-1]+route+route[1:4]
        draw.line([xy(p) for p in cyclic],fill=color,width=round(width*scale),joint="curve")
        svg.append(f'<polygon points="{svg_points(route[:-1])}" fill="none" stroke="{color}" stroke-width="{width}" stroke-linejoin="round"/>')
    # Passage sides never cover the road with an extended roof.
    for f in features:
        if f["kind"] not in ("arch","bridge_banks"):
            continue
        k=min(range(len(route)-1),key=lambda i:dist(f["center"],route[i]))
        a,b=route[(k-3)%(len(route)-1)],route[(k+3)%(len(route)-1)]
        p=route[k]
        length=dist(a,b)
        tangent=((b[0]-a[0])/length,(b[1]-a[1])/length)
        normal=(-tangent[1],tangent[0])
        for side in (-1,1):
            cx,cy=p[0]+normal[0]*side*24,p[1]+normal[1]*side*24
            q=[(cx+tangent[0]*u+normal[0]*v,cy+tangent[1]*u+normal[1]*v) for u,v in [(-18,-6),(18,-6),(18,6),(-18,6)]]
            draw.polygon([xy(z) for z in q],fill=f["color"])
            svg.append(f'<polygon points="{svg_points(q)}" fill="{f["color"]}"/>')
    svg.append('</svg>')
    img.resize((SIZE,SIZE),Image.Resampling.LANCZOS).save(target/"route-guide.png")
    (target/"route-guide.svg").write_text("\n".join(svg),encoding="utf-8")


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    for ident,return_controls in RETURNS.items():
        if len(sys.argv)>1 and ident not in sys.argv[1:]:
            continue
        source=ROOT/"RouteGuides"/ident/"route.json"
        old=json.loads(source.read_text(encoding="utf-8"))
        world=PALETTES[ident]
        features=old["landmarks"]
        # Move only a conflicting incidental outcrop; biome inventory is unchanged.
        if ident=="07_storm_coast":
            features[0]["center"]=[275,300]
        if ident=="03_amethyst_grotto":
            features[0]["center"]=[770,500]
            features[0]["radii"]=[37,65]
        if ident=="09_volcanic_garden":
            features[0]["center"]=[650,715]
            features[0]["radii"]=[45,30]
        core=old["route"]["effectiveControlPoints"]
        controls=core+[list(p) for p in return_controls]
        route=periodic_spline(controls)
        samples,length=resample(route,4)
        outline,land_added_percent=expand_outline(old["island"]["outlineSamples"],route)
        margin=outline_clearance(route,outline)
        changes,radius,signs=turns(samples)
        crossing=intersections(route[::3]+[route[-1]])
        separation=nonlocal_separation(samples)
        feature_checks=feature_clearances(features,route,outline)
        problems=[f for f in feature_checks if not f["intentionalShortPassage"] and (not f["insideIsland"] or f["overlap"])]
        print(f'{ident}: lap={length:.1f}; radius={radius:.1f}; shore={margin:.1f}; nonlocal={separation:.1f}; crossings={crossing}; turns={changes}; addedLand={land_added_percent:.1f}%; landmarkIssues={[f["name"] for f in problems]}',flush=True)
        assert crossing==0,f'{ident}: road crossing'
        assert radius>=40,f'{ident}: hairpin radius {radius}'
        assert separation>=ROAD_WIDTH+12,f'{ident}: road sections overlap or approach too closely ({separation})'
        assert margin>=29.5,f'{ident}: insufficient land shoulder'
        assert changes>=3,f'{ident}: not enough alternating interior bends'
        assert not problems,f'{ident}: landmark clashes {problems}'
        pads=building_pads(outline,route,features)
        target=OUT/ident
        target.mkdir(parents=True,exist_ok=True)
        seam=seam_derivatives(controls)
        data={
          "schemaVersion":"2.1-closed","referenceOnly":True,"id":ident,"name":old["name"],
          "provenance":{"openReference":str(source),"openReferenceUnchanged":True,"change":"Keep winding interior core, add a biome-specific generous return corridor."},
          "coordinates":{"units":"guide units","viewBox":[VIEW_ORIGIN,VIEW_ORIGIN,VIEW_SIZE,VIEW_SIZE],"origin":"top-left of original 1000-unit design","x":"right","y":"down"},
          "route":{"type":"single uninterrupted CLOSED physical ribbon","closed":True,"repeatable":True,
                   "interpolation":"Uniform periodic cubic B-spline; C2 continuous at every knot and the lap seam.","continuity":"C2 periodic", "roadWidthGuideUnits":ROAD_WIDTH,
                   "controlPoints":controls,"retainedInteriorCoreControlCount":len(core),"returnControlCount":len(return_controls),"returnDescription":RETURN_DESCRIPTIONS[ident],
                   "lengthGuideUnits":round(length,3),"lapStartReferenceOnly":[round(v,3) for v in samples[0][:2]],
                   "samples":[{"s":round(p[2],3),"t":round(p[2]/length,6),"x":round(p[0],3),"y":round(p[1],3)} for p in samples],
                   "arcLengthSamplingIntervalApprox":4,"finalSampleDuplicatesFirstForClosure":True,
                   "seam":{"position":seam["position"],"firstDerivative":seam["firstDerivative"],"secondDerivative":seam["secondDerivative"],"positionError":0.0,"tangentError":0.0,"curvatureError":0.0},
                   "selfCrossings":crossing,"minimumMeasuredBendRadiusGuideUnits":round(radius,2),"minimumRoadEdgeToShoreGuideUnits":round(margin,2),
                   "minimumNonlocalCenterlineSeparationGuideUnits":round(separation,2),"nonlocalCheckExcludesArcDistanceBelow":90,
                   "alternatingTurnSignChanges":changes,"turnSignsImageCoordinates":signs},
          "island":{"outlineSamples":[[round(p[0],3),round(p[1],3)] for p in outline],"uniformFootprintScaleFromOpen":1.0,"addedLandAreaPercent":round(land_added_percent,2),"shape":"Original biome land retained; terrain added locally only where required for a generous return shoulder. No global silhouette scaling."},
          "journeySubzones":old["journeySubzones"],"landmarks":features,"landmarkClearanceChecks":feature_checks,"technologyBuildingPads":pads,
          "repeatableLapPlan":{"referenceOnly":True,"songLengthIndependent":True,"rule":"Advance distance along the lap; sample at distance modulo total lap length. Continue for any song duration, repeating as needed.",
                               "suggestedRuntimeDistanceExpression":"wrappedDistance = totalTravelledDistance % routeLength", "fixedSongDurationSeconds":None,
                               "speedOrScaleChoice":"Choose eventual Unity scale and travel speed independently; this reference makes no fixed duration promise."},
          "artHandoff":{"noEndpointDots":True,"noStartMarker":True,"noArrows":True,"noBakedLabels":True,"schematicColorsAndPadsAreNotMandatory":True,
                        "road":"One wide continuous complex interior circuit. Never simplify to a shoreline ring or tile track. Road joins itself smoothly and has no physical ends.",
                        "passages":"Only a short open-ended stone arch or bridge-like portal; both mouths and the continuous road remain visible. No long cave roofs or enclosed tunnels.",
                        "technology":"Place 2–4 substantial technology buildings in off-road clear areas. Pads indicate broad candidate envelopes; preserve the full return road.",
                        "limits":"Flat-plan geometry checks only; final art and reconstructed 3D geometry still need collision, elevation, camera occlusion and runtime checks."}}
        (target/"route.json").write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding="utf-8")
        render(world,features,pads,outline,route,target)


if __name__=="__main__":
    main()
