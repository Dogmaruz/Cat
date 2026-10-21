"""NumPy/librosa counterpart of UVR MDX inference; no Torch/TensorFlow."""
from pathlib import Path
import argparse,hashlib,json,time
import numpy as np
import librosa,soundfile as sf
import onnxruntime as ort

HERE=Path(__file__).resolve().parent
args=argparse.ArgumentParser();args.add_argument('--audio',type=Path,default=HERE.parent.parent/'full.wav');args.add_argument('--model',type=Path,default=HERE/'UVR-MDX-NET-Voc_FT.onnx');args.add_argument('--model-data',type=Path,default=HERE/'uvr_model_data.json');args.add_argument('--out',type=Path,default=HERE);args=args.parse_args()
ROOT=args.out;ROOT.mkdir(parents=True,exist_ok=True)
model=args.model
model_hash=hashlib.md5(model.read_bytes()[-10000*1024:]).hexdigest()
metadata=json.loads(args.model_data.read_text())[model_hash]
fft=metadata['mdx_n_fft_scale_set'];dimf=metadata['mdx_dim_f_set'];dimt=2**metadata['mdx_dim_t_set'];hop=1024
chunk=hop*(dimt-1);trim=fft//2;gen=chunk-2*trim
x,sr=sf.read(args.audio,dtype='float32',always_2d=True);assert sr==44100 and x.shape[1]==2,'Expected44.1kHzstereoPCM; resample source explicitly before separation.'
x=x.T;original_length=x.shape[1]
session_options=ort.SessionOptions();session_options.intra_op_num_threads=4
session=ort.InferenceSession(str(model),sess_options=session_options,providers=['CPUExecutionProvider'])
assert session.get_inputs()[0].shape[1:]==[4,dimf,dimt]

def forward(wave):
    z=librosa.stft(wave,n_fft=fft,hop_length=hop,window='hann',center=True,pad_mode='reflect')
    return z,np.stack((z[0].real,z[0].imag,z[1].real,z[1].imag))[None,:,:dimf,:].astype(np.float32)
def inverse(pred):
    c=np.stack((pred[0,0]+1j*pred[0,1],pred[0,2]+1j*pred[0,3]))
    c=np.pad(c,((0,0),(0,fft//2+1-dimf),(0,0)))
    return librosa.istft(c,hop_length=hop,win_length=fft,window='hann',center=True,length=chunk).astype(np.float32)

probe=x[:,:chunk]
z,_=forward(probe);roundtrip=librosa.istft(z,hop_length=hop,win_length=fft,window='hann',center=True,length=chunk)
maxerr=float(np.max(abs(probe-roundtrip)));assert maxerr<1e-5,maxerr
print('Metadata/shape and full-bin STFT identity verified',metadata,'roundtrip max error',maxerr,flush=True)
pad=gen+trim-original_length%gen
mix=np.pad(x,((0,0),(trim,pad)))
result=np.zeros_like(mix);divider=np.zeros(mix.shape[1],dtype=np.float32)
starts=list(range(0,mix.shape[1],gen));started=time.time()
for i,start in enumerate(starts):
    end=min(start+chunk,mix.shape[1]);actual=end-start
    part=np.pad(mix[:,start:end],((0,0),(0,chunk-actual)))
    _,spec=forward(part);assert spec.shape==(1,4,dimf,dimt),spec.shape
    spec[:,:,:3,:]=0
    pred=session.run(None,{'input':spec})[0]
    wave=inverse(pred)
    window=np.hanning(actual).astype(np.float32)
    result[:,start:end]+=wave[:,:actual]*window
    divider[start:end]+=window
    print('Chunk',i+1,'/',len(starts),'elapsed',round(time.time()-started,1),flush=True)
vocals=(result/np.maximum(divider,1e-12))[...,trim:trim+original_length]*metadata['compensate']
assert vocals.shape==x.shape and np.isfinite(vocals).all()
instrumental=x-vocals
sf.write(ROOT/'vocals_mdx.wav',vocals.T,sr,subtype='FLOAT')
sf.write(ROOT/'instrumental_residual.wav',instrumental.T,sr,subtype='FLOAT')
stats={'modelHashAlgorithm':'MD5 last10000*1024bytes','modelHash':model_hash,'metadata':metadata,'modelSha256':hashlib.sha256(model.read_bytes()).hexdigest(),'sourcePcmSha256':hashlib.sha256(args.audio.read_bytes()).hexdigest(),'sampleRate':sr,'frames':original_length,'duration':original_length/sr,'chunk':chunk,'trim':trim,'hop':hop,'overlap':'UVR Default; step=chunk-n_fft; Hann overlap-add','denoise':False,'stftIdentityMaximumError':maxerr,'elapsedSeconds':time.time()-started,'stereoRmsMix':float(np.sqrt(np.mean(x*x))),'stereoRmsVocals':float(np.sqrt(np.mean(vocals*vocals))),'maxAbsoluteVocals':float(np.max(abs(vocals))),'warning':'Predicted vocal source from learned separation, not ground truth. Synthetic meows can be retained or rejected differently from human vocals. No human listening was available.'}
(ROOT/'separation.json').write_text(json.dumps(stats,indent=2),encoding='utf8')
print('Saved vocals and residual',json.dumps(stats,indent=2),flush=True)
