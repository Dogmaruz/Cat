"""Reusable offline note/phrase evidence from a predicted vocal stem.

Example:
 python analyze_stem.py --audio vocals_mdx.wav --out ./stem_analysis
No human-listening or vocal-identity claim is made by this program.
"""
from pathlib import Path
import argparse,hashlib,json,time,importlib.metadata
import numpy as np
import scipy
from scipy.signal import find_peaks
from scipy.ndimage import median_filter,maximum_filter1d,minimum_filter1d
import librosa,soundfile as sf
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from basic_pitch.inference import Model,predict
import basic_pitch

args=argparse.ArgumentParser();args.add_argument('--audio',type=Path,default=Path(__file__).parent/'vocals_mdx.wav');args.add_argument('--out',type=Path,default=Path(__file__).parent/'stem_analysis');args.add_argument('--source-score',type=Path,default=Path(__file__).parent.parent/'score_candidates.json');args.add_argument('--reuse-features',action='store_true',help='Explicitly reuse existing pYIN cache only when the source stem is unchanged');args=args.parse_args()
OUT=args.out;OUT.mkdir(parents=True,exist_ok=True)
def dump(name,value):(OUT/name).write_text(json.dumps(value,ensure_ascii=False,indent=2,allow_nan=False),encoding='utf8')
def spans(mask,times,minimum=.15):
    changes=np.r_[0,np.flatnonzero(np.diff(mask.astype(int)))+1,len(mask)];result=[]
    for a,b in zip(changes[:-1],changes[1:]):
        if mask[a] and (b-a)*hop/sr>=minimum:result.append({'start':round(float(times[a]),4),'end':round(float(min(duration,b*hop/sr)),4),'duration':round(float((b-a)*hop/sr),4)})
    return result

started=time.time();audio,original_sr=sf.read(args.audio,dtype='float32',always_2d=True);duration=len(audio)/original_sr
print('BasicPitch ONNX stem inference',flush=True)
model=Path(basic_pitch.__file__).parent/'saved_models/icassp_2022/nmp.onnx'
outputs,midi,events=predict(args.audio,Model(model),onset_threshold=.45,frame_threshold=.3,minimum_note_length=95,minimum_frequency=130.8128,maximum_frequency=1567.982)
notes=[{'start':round(float(a),4),'end':round(float(b),4),'midi':int(p),'strength':round(float(s),4)} for a,b,p,s,_ in events];notes.sort(key=lambda n:(n['start'],n['midi']))
dump('basic_pitch_notes.json',notes);np.savez_compressed(OUT/'basic_pitch_outputs.npz',**outputs);midi.write(str(OUT/'basic_pitch_hypothesis.mid'))
print('Raw stem notes',len(notes),'seconds',round(time.time()-started,1),flush=True)
sr=22050;hop=256;y=librosa.resample(audio.mean(axis=1),orig_sr=original_sr,target_sr=sr)
times=librosa.times_like(librosa.feature.rms(y=y,frame_length=1024,hop_length=hop)[0],sr=sr,hop_length=hop)
rms=librosa.feature.rms(y=y,frame_length=1024,hop_length=hop)[0];db=librosa.amplitude_to_db(rms,ref=1,top_db=None)
onset=librosa.onset.onset_strength(y=y,sr=sr,hop_length=hop,n_fft=1024,fmin=180,fmax=5000,aggregate=np.mean)
peaks,_=find_peaks(onset,distance=8,prominence=max(.08,float(np.max(onset))*.025))
peak_times=librosa.frames_to_time(peaks,sr=sr,hop_length=hop)
print('pYIN on predicted vocals G3..G6',flush=True)
if args.reuse_features:
    cache=np.load(OUT/'stem_features.npz');f0=cache['f0'];voiced=cache['voiced'];prob=cache['voiced_probability']
else:
    f0,voiced,prob=librosa.pyin(y,fmin=librosa.note_to_hz('G3'),fmax=librosa.note_to_hz('G6'),sr=sr,frame_length=2048,hop_length=hop,n_thresholds=30,resolution=.15)
ft=librosa.times_like(f0,sr=sr,hop_length=hop);pm=librosa.hz_to_midi(f0);smooth=median_filter(np.nan_to_num(pm,nan=0),size=5);smooth[~voiced]=np.nan
np.savez_compressed(OUT/'stem_features.npz',times=times,rms=rms,rms_dbfs=db,onset=onset,onset_times=peak_times,pyin_times=ft,f0=f0,midi=smooth,voiced_probability=prob,voiced=voiced)

# Full-range source activity includes low-level reverb; separately mark stronger
# foreground activity. These thresholds do not establish semantic vocal identity.
relative_threshold=float(np.max(db)-30)
active=maximum_filter1d(db>relative_threshold,size=9);active=minimum_filter1d(active,size=5)
active_spans=spans(active,times,.18);gaps=spans(~active,times,.25)
strong_spans=spans(maximum_filter1d(db>float(np.max(db)-20),size=5),times,.18)

# Add evidence to every polyphonic event, never silently label all as the lead.
for n in notes:
    ix=(ft>=n['start'])&(ft<n['end']);valid=ix&np.isfinite(smooth)
    agree=float(np.mean((abs(np.nan_to_num(smooth[ix],nan=-100)-n['midi'])<.75)*prob[ix])) if ix.any() else 0.
    n['pyinAgreement']=round(agree,3)
    spread=float(np.nanpercentile(smooth[valid],90)-np.nanpercentile(smooth[valid],10)) if valid.any() else None
    n['pitchSpreadSemitones']=None if spread is None else round(spread,3)
    near=np.flatnonzero(abs(peak_times-n['start'])<=.075)
    n['onsetStrength']=round(float(np.max(onset[peaks[near]])),3) if len(near) else 0.
    n['onsetDistance']=round(float(np.min(abs(peak_times-n['start']))),4) if len(peak_times) else None
    n['confidence']=round(float(np.clip(.22+.55*n['strength']+.25*agree,.2,.90)),3)
dump('notes_with_evidence.json',notes)

# Select the strongest continuous pitch from the predicted vocal stem, allowing
# an explicit rest state. Retain same-pitch reattacks supported by onset flux.
dt=.01;t=np.arange(0,duration,dt);pitches=np.arange(55,85);rest=len(pitches);nstate=rest+1
power=np.zeros((len(t),rest),dtype=np.float32)
for n in notes:
    if 55<=n['midi']<=84 and n['strength']>=.27:
        a=max(0,int(np.ceil(n['start']/dt)));b=min(len(t),int(np.floor(n['end']/dt))+1);j=n['midi']-55
        power[a:b,j]=np.maximum(power[a:b,j],n['strength'])
pf=np.interp(t,ft,np.nan_to_num(smooth,nan=0));pp=np.interp(t,ft,prob)
emission=np.full((len(t),nstate),-3.,dtype=np.float32)
for j,p in enumerate(pitches):
    emission[:,j]=power[:,j]*2-.44+.45*np.exp(-((pf-p)/.7)**2)*pp
    emission[power[:,j]==0,j]=-3
emission[:,rest]=.12
transition=-.17-.008*np.minimum(abs(np.arange(nstate)[:,None]-np.arange(nstate)[None,:]),12);np.fill_diagonal(transition,0);transition[rest,:]=transition[:,rest]=-.13;transition[rest,rest]=0
state_score=emission[0].astype(float);back=np.zeros((len(t),nstate),dtype=np.uint8)
for i in range(1,len(t)):
    opt=state_score[:,None]+transition;back[i]=np.argmax(opt,axis=0);state_score=emission[i]+np.max(opt,axis=0)
path=np.empty(len(t),dtype=np.uint8);path[-1]=np.argmax(state_score)
for i in range(len(t)-1,0,-1):path[i-1]=back[i,path[i]]
changes=np.r_[0,np.flatnonzero(np.diff(path)!=0)+1,len(t)];lead=[]
onset_cut=float(np.quantile(onset[peaks],.30)) if len(peaks) else 0
for a,b in zip(changes[:-1],changes[1:]):
    j=path[a]
    if j==rest or (b-a)*dt<.1:continue
    pitch=int(pitches[j]);start=t[a];end=min(duration,b*dt)
    attacks=[]
    for n in notes:
        if n['midi']==pitch and n['strength']>=.42 and n['onsetStrength']>=onset_cut and start+.12<n['start']<end-.12:
            if not attacks or n['start']-attacks[-1]>=.13:attacks.append(n['start'])
    boundaries=[start]+attacks+[end]
    for x,z in zip(boundaries[:-1],boundaries[1:]):
        if z-x<.095:continue
        ix=(t>=x)&(t<z);strength=float(np.mean(power[ix,j]));agree=float(np.mean((abs(pf[ix]-pitch)<.75)*pp[ix]))
        nearby=np.flatnonzero(abs(peak_times-x)<=.075);os=float(np.max(onset[peaks[nearby]])) if len(nearby) else 0.
        valid=(ft>=x)&(ft<z)&np.isfinite(smooth);pitchspread=float(np.nanpercentile(smooth[valid],90)-np.nanpercentile(smooth[valid],10)) if valid.any() else None
        lead.append({'start':round(float(x),4),'end':round(float(z),4),'midi':pitch,'strength':round(strength,3),'confidence':round(float(np.clip(.25+.5*strength+.25*agree,.2,.90)),3),'pyinAgreement':round(agree,3),'onsetStrength':round(os,3),'pitchSpreadSemitones':None if pitchspread is None else round(pitchspread,3),'samePitchReattack':bool(x!=start)})
dump('lead_notes.json',lead)

# Candidate sustains require stable F0 and lack a strong internal reattack. Raw
# model spans are retained so an editor can reject or merge fragments explicitly.
holds=[]
for n in lead:
    internal=(peak_times>n['start']+.12)&(peak_times<n['end']-.12)
    internal_strength=float(np.max(onset[peaks[internal]])) if internal.any() else 0
    if n['end']-n['start']>=.55 and n['pyinAgreement']>=.35 and n['pitchSpreadSemitones'] is not None and n['pitchSpreadSemitones']<=1.2 and internal_strength<max(onset_cut,n['onsetStrength']*.6):
        holds.append(dict(n,reason='Stable predicted-vocal pitch with no stronger internal spectral reattack; separation identity remains uncertain.'))
dump('sustain_candidates.json',holds)
attacks=[]
for n in lead:
    if n['onsetStrength']>=onset_cut and n['strength']>=.42:
        attacks.append(dict(n,reason='Dominant note in predicted vocal stem with a nearby spectral attack; repeated same-pitch articulation preserved.'))
dump('attack_candidates.json',attacks)

reference=json.loads(args.source_score.read_text())
score={k:reference[k] for k in ['schemaVersion','id','sourceSha256','audioDuration','sampleRate','bpm','beats','phrases']};score['notes']=lead;score['cues']=[]
for n in attacks:
    hold=next((h for h in holds if abs(h['start']-n['start'])<.001),None)
    score['cues'].append({'time':n['start'],'endTime':hold['end'] if hold else n['start'],'kind':'Hold' if hold else 'Jump','lane':round(float(np.clip((n['midi']-68)*.1,-1,1)),3),'points':[],'reason':hold['reason'] if hold else n['reason'],'phraseId':next((p['id'] for p in reference['phrases'] if p['start']<=n['start']<p['end']),''),'confidence':n['confidence']})
score['analysisCaution']='Predicted vocal stem, no listening. Synthetic meows and piano may leak across separation; notes/cues are measured candidates, not a finalized playable route. Confidence is not calibrated. Pitch-derived lanes are provisional. Do not add scored supports without musical evidence.'
dump('score_stem_candidates.json',score)
version={p:importlib.metadata.version(p) for p in ['numpy','scipy','librosa','soundfile','onnxruntime','basic-pitch','matplotlib','pretty_midi']}
summary={'audio':str(args.audio.resolve()),'stemSha256':hashlib.sha256(args.audio.read_bytes()).hexdigest(),'duration':duration,'sampleRate':original_sr,'versions':version,'elapsedSeconds':time.time()-started,'rawPolyphonicNotes':len(notes),'leadIntervals':len(lead),'attackCandidates':len(attacks),'sustainCandidates':len(holds),'activityThresholdDbfs':relative_threshold,'activitySpans':active_spans,'strongActivitySpans':strong_spans,'activityGaps':gaps,'onsetStrengthThreshold':onset_cut,'pyinMedianVoicedProbability':float(np.median(prob[voiced])) if voiced.any() else 0,'voicedFrameFraction':float(np.mean(voiced)),'caution':score['analysisCaution']}
dump('summary.json',summary)

fig,ax=plt.subplots(3,1,figsize=(18,10),sharex=True,gridspec_kw={'height_ratios':[3,1,1]})
for n in notes:ax[0].plot([n['start'],n['end']],[n['midi'],n['midi']],color='gray',alpha=.2,lw=1)
for n in lead:ax[0].plot([n['start'],n['end']],[n['midi'],n['midi']],color=plt.cm.viridis(n['confidence']),lw=2.3)
ax[0].scatter(ft,smooth,c=prob,s=1,cmap='plasma',alpha=.35);ax[0].set_ylim(54,90);ax[0].set_ylabel('MIDI pitch');ax[0].set_title('Predicted vocal stem — dominant-note HYPOTHESIS; no human listening / vocal identity guarantee')
ax[1].plot(times,db,color='darkmagenta',lw=.7);ax[1].axhline(relative_threshold,color='gray',ls='--');ax[1].set_ylabel('Stem RMS dBFS');ax[1].set_ylim(-75,0)
ax[2].plot(times[:len(onset)],onset,color='purple',lw=.7)
for n in attacks:ax[2].vlines(n['start'],0,max(1,n['onsetStrength']),color='green',alpha=.5,lw=.8)
for h in holds:ax[2].axvspan(h['start'],h['end'],color='orange',alpha=.3)
ax[2].set_ylabel('Spectral attacks');ax[2].set_xlabel('Audio time (seconds)')
for a in ax:a.grid(alpha=.2)
ax[2].set_xlim(0,duration);fig.tight_layout();fig.savefig(OUT/'stem_piano_roll_full.png',dpi=145);ax[2].set_xlim(0,30);fig.savefig(OUT/'stem_piano_roll_first30.png',dpi=155);plt.close(fig)
print('Complete',json.dumps({k:summary[k] for k in ['rawPolyphonicNotes','leadIntervals','attackCandidates','sustainCandidates','voicedFrameFraction','elapsedSeconds']}),flush=True)
