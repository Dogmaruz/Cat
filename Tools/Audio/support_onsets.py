"""Measured spectral attacks in mix/residual, no periodic gap filling."""
from pathlib import Path
import argparse,json
import numpy as np
import librosa,soundfile as sf
from scipy.signal import find_peaks
HERE=Path(__file__).resolve().parent
parser=argparse.ArgumentParser();parser.add_argument('--audio',type=Path,default=HERE.parent.parent/'full.wav');parser.add_argument('--residual',type=Path,default=HERE/'instrumental_residual.wav');parser.add_argument('--out',type=Path,default=HERE/'support_onsets.json');args=parser.parse_args()
sources={'mix':args.audio,'instrumental_residual':args.residual}
result={}
for label,p in sources.items():
    y,sr=sf.read(p,dtype='float32',always_2d=True);y=librosa.resample(y.mean(axis=1),orig_sr=sr,target_sr=22050);sr=22050;hop=256
    flux=librosa.onset.onset_strength(y=y,sr=sr,hop_length=hop,n_fft=1024,fmin=80,fmax=10000)
    frames=librosa.onset.onset_detect(onset_envelope=flux,sr=sr,hop_length=hop,units='frames',backtrack=False,pre_max=3,post_max=3,pre_avg=8,post_avg=8,wait=11,delta=.06)
    rms=librosa.feature.rms(y=y,frame_length=1024,hop_length=hop)[0]
    ev=[]
    for f in frames:
        strength=float(flux[f]);t=float(librosa.frames_to_time(f,sr=sr,hop_length=hop));near=rms[max(0,f-2):min(len(rms),f+3)]
        ev.append({'time':round(t,4),'strength':round(strength,4),'relativeStrength':round(strength/max(float(np.max(flux)),1e-12),4),'localRmsDbfs':round(float(librosa.amplitude_to_db(np.max(near),ref=1,top_db=None)),2),'source':label,'reason':'Measured positive log-Mel spectral change; accompaniment/voice identity unverified. No metrical grid insertion.'})
    result[label]=ev
    print(label,len(ev),flush=True)
result['caution']='These are actual measured changes, not human-confirmed audible accents. Residual can retain vocal leakage; low-strength/low-energy events need editorial review. Prefer fewer phrase-appropriate supports instead of filling every peak.'
args.out.parent.mkdir(parents=True,exist_ok=True)
args.out.write_text(json.dumps(result,indent=2),encoding='utf8')
