import json,pathlib,sys
work=pathlib.Path(__file__).resolve().parent
project=work.parents[1]
out=project.parent/'Docs/Clone/ChartCalibration/V12'
sys.path.insert(0,str(project/'Library/OriginalChartCalibration/plot-deps'))
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.patches import Patch
rows=json.loads((work/'summary.json').read_text(encoding='utf-8'))
names=['Disconnect','Can Can','I Just Wanna Dance','Moonlight Clouds','Unstoppable','Twinkle Little Star','Moonlight','LABUBU','Für Elise','Jingle Bells','Ode to Joy','Canon in D','Aaj Ki Raat','Bajo El Sol','Baby Shark']
fig,axes=plt.subplots(1,3,figsize=(15,10),gridspec_kw={'width_ratios':[1.25,1.15,1]})
colors={'baseline':'#6687b8','candidate':'#e49a39'}
for ax in axes:
    ax.set_ylim(len(rows)-.45,-.75);ax.set_yticks(range(len(rows)))
    ax.grid(axis='x',alpha=.15);ax.set_axisbelow(True)
    ax.spines[['top','right','left']].set_visible(False);ax.tick_params(axis='y',length=0)
for ax,key,title in zip(axes[:2],['shortHolds','follows'],['Короткие удержания','Прокаты']):
    for i,row in enumerate(rows):
        ref=row['candidate']['types'][key]['referenceCount']
        for stage,shift in [('baseline',-.18),('candidate',.18)]:
            m=row[stage]['types'][key];tp=m['truePositive'];fp=m['falsePositive'];y=i+shift
            ax.barh(y,tp,height=.29,color=colors[stage])
            if fp:ax.barh(y,fp,left=tp,height=.29,color='#c45a61',hatch='//')
        ax.plot([ref,ref],[i-.36,i+.36],color='#78818a',lw=1)
        m=row['candidate']['types'][key]
        occupied=max([ref]+[row[stage]['types'][key]['truePositive']+row[stage]['types'][key]['falsePositive'] for stage in ['baseline','candidate']])
        ax.text(occupied+.45,i,f"{m['truePositive']}/{ref}; +{m['falsePositive']}",va='center',fontsize=8,color='#33424c')
    ax.set_title(title,fontsize=13,pad=14);ax.set_xlabel('Верные и лишние; отметка — эталон',fontsize=9)
axes[0].set_yticklabels(names,fontsize=10);axes[1].set_yticklabels([])
for i,row in enumerate(rows):
    b=row['baseline']['eventF1_80ms'];c=row['candidate']['eventF1_80ms']
    axes[2].plot([b,c],[i,i],color='#a1a8ad',lw=1.3)
    axes[2].scatter(b,i,color=colors['baseline'],s=31,zorder=3)
    axes[2].scatter(c,i,color=colors['candidate'],s=31,zorder=4,marker='D')
axes[2].set_yticklabels([]);axes[2].set_title('F1 всех событий, допуск 80 мс',fontsize=11,pad=14)
axes[2].set_xlim(.45,1);axes[2].set_xlabel('Сам по себе F1 не подтверждает тип',fontsize=9)
fig.suptitle('Генератор v12: больше распознанных поддержек',fontsize=16,y=.98)
fig.legend(handles=[Patch(color=colors['baseline'],label='v11 — база'),Patch(color=colors['candidate'],label='v12 — кандидат'),Patch(facecolor='#c45a61',hatch='//',label='Лишние предложения')],loc='lower center',ncol=3,bbox_to_anchor=(.53,.015),frameon=False)
fig.text(.19,.077,'Все 15 записей известны: это настройка и регрессионная проверка, без независимого теста на новой музыке.\nВ каждой паре сверху v11, снизу v12. Правильное совпадение требует типа и границ интервала.\nБоковые траектории остаются самостоятельной хореографией.',fontsize=9,color='#43515b')
fig.tight_layout(rect=(.01,.13,1,.94));fig.savefig(out/'typed-results.png',dpi=160,facecolor='white');plt.close(fig)
print(out/'typed-results.png')
