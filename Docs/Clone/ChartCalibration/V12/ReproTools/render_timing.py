import json,pathlib,sys
work=pathlib.Path(__file__).resolve().parent
project=work.parents[1]
sys.path.insert(0,str(project/'Library/OriginalChartCalibration/plot-deps'))
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
timing=work/'Timing'
captures=[json.loads((timing/name).read_text(encoding='utf-8-sig')) for name in
          ['jingle-original-before.json','jingle-v11-before.json','jingle-v11-buffer512-pose.json']]
labels=['Исходная карта\n1024','Карта v11\n1024','Та же v11\n512']
fig,axes=plt.subplots(1,2,figsize=(11,5.5))
colors=['#92a8c5','#e0ab55','#69afa6']
for ax,stage,key,title in zip(axes,['score-contact','effect-submit'],['contactMinusNominalMs','submitMinusNominalDspMs'],
        ['Обычное касание после метки карты','Отправка звука после метки карты']):
    values=[[r[key] for r in capture['contacts'] if r['stage']==stage and r['kind']=='Jump'] for capture in captures]
    boxes=ax.boxplot(values,tick_labels=labels,patch_artist=True,widths=.55,showfliers=False,whis=(5,95),medianprops=dict(color='#273c50',linewidth=1.8))
    for patch,color in zip(boxes['boxes'],colors):patch.set_facecolor(color);patch.set_alpha(.85)
    ax.set_title(title,fontsize=12);ax.set_ylabel('мс, программное время')
    ax.grid(axis='y',alpha=.2);ax.set_axisbelow(True);ax.spines[['top','right']].set_visible(False)
    for i,v in enumerate(values):
        ordered=sorted(v);n=len(v);median=(ordered[(n-1)//2]+ordered[n//2])/2
        ax.text(i+1,max(v)+.6,f'n={n}; медиана {median:.2f}',ha='center',fontsize=8,color='#334d60')
    ax.set_ylim(top=max(max(v) for v in values)+3)
fig.suptitle('Jingle Bells: контакт и звук разделены в измерениях',fontsize=15)
fig.text(.10,.05,'Первый столбец — восстановленная карта в нашем runtime, не захват исходного приложения.\nЛиния — медиана, усы — 5–95%; физическая задержка слышимого звука здесь не измерена.',fontsize=9,color='#43515b')
fig.tight_layout(rect=(0,.13,1,.93))
out=project.parent/'Docs/Clone/ChartCalibration/V12/Timing/timing-comparison.png'
fig.savefig(out,dpi=160,facecolor='white');plt.close(fig)
print(out)
