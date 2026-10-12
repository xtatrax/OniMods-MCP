const fs=require('fs'),vm=require('vm'),assert=require('assert');
const path=require('path').resolve(__dirname,'../mods/OniMcp')+'/';
async function run(lang){
 const dictionary=JSON.parse(fs.readFileSync(path+'Localization/Catalogs/human-map.'+lang+'.json'));
 const labels=Object.fromEntries(Object.entries(dictionary).map(([k,v])=>[k.slice('humanMap.'.length),v]));
 const context2d=new Proxy({},{get:()=>()=>{}});
 function element(){return {textContent:'',children:[],style:{},checked:false,value:'terrain',clientWidth:100,clientHeight:100,selectedOptions:[{text:'Elements'}],append(x){this.children.push(x)},replaceChildren(){this.children=[]},getContext(){return context2d},addEventListener(k,f){this[k]=f},getBoundingClientRect(){return {left:0,top:0}},setPointerCapture(){}}}
 const elements=Object.fromEntries(['map','layer','buildings','fog','zoom','selection','details','legend','refresh','status','world','hover','plus','minus','fit','colony','auto'].map(k=>[k,element()]));
 const snapshot={width:1,height:1,xMin:10,yMin:20,worldId:0,worldName:'テストコロニー',cycle:38,cyclePercent:84.8,paused:true,capturedAt:'2026-10-05T15:00:00Z',rowOrder:'y_ascending',elements:[1],temperatures:[22],masses:[1],visible:[1],networks:[0],palette:[{id:'Unavailable',name:'取得不可',color:'#000',state:'unknown'},{id:'Oxygen',name:'酸素',color:'#fff',state:'gas'}],buildings:[],dupes:[],gameElementNames:{Oxygen:'酸素',ContaminatedOxygen:'汚染酸素'}};
 const sandbox={OniHumanMapLabels:labels,document:{getElementById:k=>elements[k],createElement:element,hidden:false},devicePixelRatio:1,ResizeObserver:class{observe(){}},setInterval(){},fetch:async()=>({ok:true,json:async()=>snapshot}),AbortSignal:{timeout(){}}};sandbox.window=sandbox;
 vm.runInNewContext(fs.readFileSync(path+'Browser/human-map.js','utf8'),sandbox);
 await new Promise(resolve=>setImmediate(resolve));
 assert(elements.status.textContent.includes(labels.paused));
 assert.throws(()=>sandbox.OniHumanMap.validate({...snapshot,width:0}),e=>e.message===labels.invalidDimensions);
 assert.throws(()=>sandbox.OniHumanMap.validate({...snapshot,masses:[]}),e=>e.message===labels.missingData+'masses');
 assert(elements.world.textContent.includes('テストコロニー'));
 elements.layer.value='oxygen';elements.layer.onchange();
 const all=el=>[el.textContent,...el.children.flatMap(all)];
 assert(all(elements.legend).includes('酸素'));assert(all(elements.legend).includes('汚染酸素'));
 assert(all(elements.legend).includes(labels.entityLegend));
 elements.map.pointerdown({button:0,clientX:50,clientY:50,pointerId:1});
 elements.map.pointerup({clientX:50,clientY:50});
 assert(all(elements.details).includes('酸素 (Oxygen)'));
 assert(all(elements.details).some(v=>v.startsWith(labels.temperatureLabel)));
 assert(all(elements.details).includes(labels.openCell));
 console.log('PASS map UI '+lang+': localized labels; native Japanese game names and Oxygen ID preserved');
}
(async()=>{for(const lang of ['en','ja','zh-Hans'])await run(lang)})().catch(e=>{console.error(e);process.exit(1)});
