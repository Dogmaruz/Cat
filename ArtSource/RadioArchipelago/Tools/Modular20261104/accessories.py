"""Prepare the supplied isolated hats/scarves; original archives stay read-only."""
from pathlib import Path
import sys, json, zipfile, math, shutil
import bpy
import numpy as np
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[4]
INPUT=ROOT/'ArtSource/RadioArchipelago/ModularCast20261102/Incoming'
OUT=ROOT/'ArtSource/RadioArchipelago/ProcessedModular'
QA=ROOT/'Docs/Design/RadioArchipelago/ModularCast20261104'

def extract(category, ident):
    z=INPUT/category/ident/(ident+'.zip'); dest=OUT/'_Sources'/category/ident
    dest.mkdir(parents=True,exist_ok=True)
    with zipfile.ZipFile(z) as arc:
        for item in arc.infolist():
            p=(dest/item.filename).resolve()
            if not p.is_relative_to(dest.resolve()): raise ValueError(item.filename)
            if item.is_dir(): p.mkdir(parents=True,exist_ok=True)
            elif not p.exists():
                p.parent.mkdir(parents=True,exist_ok=True)
                with arc.open(item) as source,p.open('wb') as target: shutil.copyfileobj(source,target)
    return dest

def image_for(src,word):
    files=list(src.rglob('*'))
    return next((p for p in files if p.is_file() and p.suffix.lower()=='.png' and word.lower() in p.stem.lower()),None)

def textures(src,dest):
    color=image_for(src,'color') or next(src.rglob('*.png'))
    normal=image_for(src,'normal'); metal=image_for(src,'metallic'); rough=image_for(src,'roughness')
    def load(path,linear=False):
        img=bpy.data.images.load(str(path),check_existing=True)
        if linear: img.colorspace_settings.name='Non-Color'
        return img
    def save(img,path,size=2048):
        if max(img.size)>size:
            factor=size/max(img.size); img.scale(max(1,int(img.size[0]*factor)),max(1,int(img.size[1]*factor)))
        img.filepath_raw=str(path);img.file_format='PNG';img.save()
    base=load(color);save(base,dest/'BaseColor.png')
    n=load(normal,True) if normal else bpy.data.images.new('NeutralNormal',4,4)
    if not normal:
        n.colorspace_settings.name='Non-Color';n.pixels[:]=[.5,.5,1,1]*16
    save(n,dest/'Normal.png')
    size=1024
    def channel(path,default):
        if not path:return np.full((size*size,),default,dtype=np.float32)
        img=load(path,True);img.scale(size,size)
        a=np.empty(size*size*4,dtype=np.float32);img.pixels.foreach_get(a);return a.reshape(-1,4)[:,0]
    rgba=np.zeros((size*size,4),dtype=np.float32);rgba[:,0]=channel(metal,0);rgba[:,3]=1-channel(rough,.65)
    packed=bpy.data.images.new('MetallicSmoothness',size,size);packed.colorspace_settings.name='Non-Color'
    packed.pixels.foreach_set(rgba.ravel());save(packed,dest/'MetallicSmoothness.png')
    mat=bpy.data.materials.new('Supplied_'+dest.name);mat.use_nodes=True
    bs=mat.node_tree.nodes.get('Principled BSDF')
    for img,socket in [(base,'Base Color'),(n,'Normal')]:
        node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=img
        if socket=='Normal':
            norm=mat.node_tree.nodes.new('ShaderNodeNormalMap');mat.node_tree.links.new(node.outputs['Color'],norm.inputs['Color']);mat.node_tree.links.new(norm.outputs['Normal'],bs.inputs['Normal'])
        else:mat.node_tree.links.new(node.outputs['Color'],bs.inputs[socket])
    bs.inputs['Roughness'].default_value=.65
    return mat

def aim(obj,point):obj.rotation_euler=(Vector(point)-obj.location).to_track_quat('-Z','Y').to_euler()
def render(dest,ident):
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=16
    scene.render.resolution_x=600;scene.render.resolution_y=600;scene.render.resolution_percentage=100
    if scene.world is None:scene.world=bpy.data.worlds.new('Review world')
    scene.world.color=(.3,.3,.3)
    for loc,power,size in [((-3,-4,5),500,4),((3,1,4),400,3)]:
        data=bpy.data.lights.new('Review softbox','AREA');data.energy=power;data.shape='DISK';data.size=size
        o=bpy.data.objects.new(data.name,data);scene.collection.objects.link(o);o.location=loc;aim(o,(0,0,.25))
    cam=bpy.data.objects.new('Review camera',bpy.data.cameras.new('Review camera'));scene.collection.objects.link(cam);scene.camera=cam
    cam.data.type='ORTHO';cam.data.ortho_scale=1.6
    scene.render.film_transparent=False
    for side,loc in [('front',(0,-4,1.7)),('side',(4,-.5,1.2))]:
        cam.location=loc;aim(cam,(0,0,.3));scene.render.filepath=str(dest/(ident+'_'+side+'.png'));bpy.ops.render.render(write_still=True)
    for obj in list(scene.objects):
        if obj.type in {'LIGHT','CAMERA'}:bpy.data.objects.remove(obj,do_unlink=True)

def prepare(category,ident):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    src=extract(category,ident);dest=OUT/category/ident;dest.mkdir(parents=True,exist_ok=True)
    bpy.ops.import_scene.fbx(filepath=str(next(src.rglob('*.fbx'))))
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes:o.select_set(True)
    bpy.context.view_layer.objects.active=meshes[0];bpy.ops.object.convert(target='MESH');bpy.ops.object.join()
    obj=bpy.context.object;obj.name='HatMesh' if category=='Hats' else 'ScarfMesh'
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    points=[obj.matrix_world@v.co for v in obj.data.vertices];lo=Vector(tuple(min(p[i] for p in points) for i in range(3)));hi=Vector(tuple(max(p[i] for p in points) for i in range(3)))
    scale=1/(hi.x-lo.x);center=(lo+hi)/2
    for v,p in zip(obj.data.vertices,points):v.co=(p-Vector((center.x,center.y,lo.z)))*scale
    obj.matrix_world.identity()
    before=len(obj.data.polygons)
    # Keep curved silhouettes and the user's UVs; cap needlessly dense source meshes.
    if before>12000:
        dec=obj.modifiers.new('Mobile silhouette budget','DECIMATE');dec.ratio=12000/before
        bpy.ops.object.modifier_apply(modifier=dec.name)
    mat=textures(src,dest);obj.data.materials.clear();obj.data.materials.append(mat)
    for p in obj.data.polygons:p.use_smooth=True
    vertices=[v.co for v in obj.data.vertices]
    bounds={'min':[min(v[i] for v in vertices) for i in range(3)],'max':[max(v[i] for v in vertices) for i in range(3)]}
    meta={'id':ident,'category':category,'frame':'Blender Z up, front -Y; FBX -Z/Y','bounds':bounds,'sourceBounds':{'min':list(lo),'max':list(hi)},'sourcePolygons':before,'polygons':len(obj.data.polygons),'hasSourceNormal':image_for(src,'normal') is not None,'hatBackwardTiltDegrees':14}
    # Tail authoring follows after inspection; keep the neck loop separate from moving fabric.
    if category=='Scarves':
        armdata=bpy.data.armatures.new('ScarfRig');arm=bpy.data.objects.new('ScarfRig',armdata);bpy.context.scene.collection.objects.link(arm)
        bpy.context.view_layer.objects.active=arm;arm.select_set(True);obj.select_set(False);bpy.ops.object.mode_set(mode='EDIT')
        root=armdata.edit_bones.new('scarf.root');root.head=(0,0,.2);root.tail=(0,0,.3)
        # These scarves hang down from the rear knot. The neck loop stays fixed;
        # use the downward fabric length instead of bending half the collar.
        height=max(v.z for v in vertices);upper=[v for v in vertices if v.z>height*.7]
        neck_y=(min(v.y for v in upper)+max(v.y for v in upper))*.5
        start_z=height*.57
        tail=[v for v in vertices if v.z<start_z and v.y>neck_y]
        tip=Vector((sum(v.x for v in tail)/len(tail),sum(v.y for v in tail)/len(tail),min(v.z for v in tail)))
        base=Vector((tip.x,tip.y*.75,start_z))
        chain=[]
        for i in range(4):
            bone=armdata.edit_bones.new('scarf.%02d'%i);bone.head=base.lerp(tip,i/4);bone.tail=base.lerp(tip,(i+1)/4);bone.parent=root if i==0 else chain[-1];chain.append(bone)
        bpy.ops.object.mode_set(mode='OBJECT');obj.parent=arm
        groups={n:obj.vertex_groups.new(name=n) for n in ['scarf.root']+['scarf.%02d'%i for i in range(4)]}
        for v in obj.data.vertices:
            t=max(0,min(3.999,(start_z-v.co.z)/max(.01,start_z-tip.z)*4))
            if v.co.y<=neck_y or v.co.z>=start_z:groups['scarf.root'].add([v.index],1,'REPLACE')
            else:
                k=int(t);f=t-k;groups['scarf.%02d'%k].add([v.index],1-f,'REPLACE')
                if k<3:groups['scarf.%02d'%(k+1)].add([v.index],f,'REPLACE')
                else:groups['scarf.%02d'%k].add([v.index],1,'REPLACE')
        mod=obj.modifiers.new('Fabric spring chain','ARMATURE');mod.object=arm
        meta['scarfChain']=['scarf.%02d'%i for i in range(4)]
    (dest/'Meta.json').write_text(json.dumps(meta,indent=2),encoding='utf-8')
    renderdir=QA/'AccessoryViews';renderdir.mkdir(parents=True,exist_ok=True);render(renderdir,ident)
    bpy.ops.wm.save_as_mainfile(filepath=str(dest/(ident+'.blend')))
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=str(dest/'Model.fbx'),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
    print('MODULAR_ACCESSORY_READY',category,ident,meta,flush=True)

if __name__=='__main__':
    args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
    for category in ['Hats','Scarves']:
        for folder in sorted((INPUT/category).iterdir()):
            if folder.is_dir() and (not args or folder.name in args):prepare(category,folder.name)
