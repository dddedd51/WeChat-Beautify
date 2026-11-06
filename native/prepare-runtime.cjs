// Local research build of the pinned upstream engine plus the glass extension.
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const root=path.resolve(__dirname,'..');
const source=path.join(root,'research/reference/Windows-WeChat-Theme-Studio-37d8188502ed4365ddcde928c7b2e50fda5e7226/native/runtime.c');
if(!fs.existsSync(source))throw Error('Pinned upstream source is missing. Run ./fetch-upstream.ps1 first.');
const sourceBytes=fs.readFileSync(source);
if(crypto.createHash('sha256').update(sourceBytes).digest('hex')!=='4473abd121825524676ce6dcea7fb310bd6266541d10d4039380423ff7be3a06')throw Error('Pinned upstream runtime.c failed SHA-256 verification.');
let c=sourceBytes.toString('utf8').replace(/\r\n/g,'\n');
function replace(a,b){if(c.split(a).length!==2)throw Error('Source anchor changed: '+a.slice(0,70));c=c.replace(a,b);}
replace('#include <stdlib.h>','#include <stdlib.h>\n#include <dwmapi.h>\n#include <bcrypt.h>');
replace('static HMODULE own;','static HMODULE own;\nstatic int glassEnabled, glassDensity=65, glassDark, glassReady;\nstatic unsigned glassLastGeneration;\nstatic void glassRead(void);\nstatic void glassApply(void);\nstatic void glassRestore(void);\nstatic int verifyTargetFile(void);');
replace('q->a=65535;','q->a=((c>>24)&255)*257;');
replace('unsigned c=palette[i*2+k];byte *d=', 'unsigned c=palette[i*2+k];if(glassEnabled&&i<5)c=0;byte *d=');
replace('(byte)c,255};','(byte)c,(byte)(c>>24)};');
replace('qcolor(&q,palette[i*2+m]);if(memcmp', 'qcolor(&q,glassEnabled&&i<5?0:palette[i*2+m]);if(memcmp');
replace('status.paints++;if(!p->wall', 'status.paints++;if(glassEnabled&&k==1)return 0;if(!p->wall');
// The upstream mask1 was exported but no message surface received kind 4.
replace('else if(!strcmp(s,"mmui::MainTabBar"))kind=7;attach(o,kind);', 'else if(!strcmp(s,"mmui::MainTabBar"))kind=7;else if(!strcmp(s,"mmui::ChatMessagePage"))kind=4;attach(o,kind);');
replace('static void restore(void){unsigned i;', 'static void restore(void){unsigned i;glassRestore();');
replace('qcolor(&q,((unsigned)d[0]<<16)|((unsigned)d[1]<<8)|d[2]);', 'qcolor(&q,((unsigned)d[3]<<24)|((unsigned)d[0]<<16)|((unsigned)d[1]<<8)|d[2]);');
replace('if(!status.enabled){restore();status.generation++;return;}for(m=', 'if(!status.enabled){restore();status.generation++;logStatus("disabled-restored");return;}glassRead();for(m=');
replace('qcolor(&q,palette[i*2+m]);if(!memcmp', 'qcolor(&q,glassEnabled&&i<5?0:palette[i*2+m]);if(!memcmp');
replace('if(gen!=status.generation||!paletteOwned)paletteApply();paletteNotifications=0;', 'if(gen!=status.generation||!paletteOwned)paletteApply();glassApply();paletteNotifications=0;');
replace('if(type==17||!paletteOwned)paletteApply();if(type==17)', 'if(type==17||!paletteOwned)paletteApply();glassApply();if(type==17)');
replace('if(!image)return NULL;/* PE size', 'if(!image||!verifyTargetFile())return NULL;/* PE size');
replace('wcscpy(baseConfigPath,configPath);wcscpy(previewControl,path);', 'wcscpy(baseConfigPath,configPath);logStatus("qt-testability-entered-target-verified");wcscpy(previewControl,path);');
// Use the universal C99 formatting function on MSVC too.
c=c.replace(/swprintf\(k,L/g,'swprintf(k,20,L');
replace('if(!patch(image+0x93ab420,&(void*){textPaint},8)||!patch(image+0x9883cf0,&(void*){textPaint},8)||!patch(slot,&(void*){mainEvent},8))', 'void *newText=(void*)textPaint,*newEvent=(void*)mainEvent;if(!patch(image+0x93ab420,&newText,8)||!patch(image+0x9883cf0,&newText,8)||!patch(slot,&newEvent,8))');
c+='\n'+fs.readFileSync(path.join(__dirname,'glass-extension.c'),'utf8');
fs.mkdirSync(path.join(root,'research/build'),{recursive:true});
fs.writeFileSync(path.join(root,'research/build/runtime.c'),c);
console.log('Prepared pinned runtime with Qt alpha + DWM glass extension.');
