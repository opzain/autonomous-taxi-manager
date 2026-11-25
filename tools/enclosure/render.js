const {chromium}=require('playwright');const path=require('path');
const out=path.resolve(process.argv[2]);
(async()=>{const b=await chromium.launch();
 const p=await b.newPage();
 await p.goto('file://'+out+'/templates.html'); await p.waitForTimeout(300);
 await p.pdf({path:out+'/controller-templates-1to1.pdf',preferCSSPageSize:true,printBackground:true});
 await p.goto('file://'+out+'/guide.html'); await p.waitForTimeout(300);
 await p.pdf({path:out+'/controller-build-guide.pdf',preferCSSPageSize:true,printBackground:true});
 const q=await b.newPage({viewport:{width:1600,height:1200},deviceScaleFactor:1});
 for(const n of ['render','construction','holes','inside','section']){
   await q.goto('file://'+out+'/'+n+'.svg'); const el=await q.$('svg'); await el.screenshot({path:out+'/'+n+'.png'});}
 // template pages as images for checking
 const t=await b.newPage({viewport:{width:1123,height:794},deviceScaleFactor:1});
 await t.goto('file://'+out+'/templates.html');
 const pages=await t.$$('.page'); let i=1; for(const pg of pages){await pg.screenshot({path:out+'/tpl-'+(i++)+'.png'});}
 await b.close();})();
