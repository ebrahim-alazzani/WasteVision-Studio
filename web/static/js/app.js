(() => {
  const $ = id => document.getElementById(id);
  let dataset = null, training = null, result = null, lossChart = null, accuracyChart = null, classChart = null, predictionUrl = null, polling = false;
  const chartColors = {mint:'#7be0bd', aqua:'#50c9d3', amber:'#f1b75c', muted:'#9cacaa'};

  function escapeHtml(value) { return String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c])); }
  function showError(message) { $('alert').textContent = message; $('alert').hidden = false; }
  function clearError() { $('alert').hidden = true; }
  async function request(url, options={}) {
    const response = await fetch(url, {...options, credentials:'same-origin'});
    const data = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(data.detail || `تعذر تنفيذ الطلب (${response.status}).`);
    return data;
  }
  function setService(ok, text) { $('service-label').textContent=text; $('service-dot').parentElement.classList.toggle('ok',ok); }

  function showPage(pageId, updateHash=true) {
    const page=document.getElementById(pageId);
    if(!page?.matches('[data-workspace-page]')) pageId='page-overview';
    document.querySelectorAll('[data-workspace-page]').forEach(item=>{
      const active=item.id===pageId; item.hidden=!active; item.classList.toggle('active',active);
    });
    document.querySelectorAll('.sidebar-link').forEach(item=>item.classList.toggle('active',item.dataset.page===pageId));
    if(updateHash && location.hash!==`#${pageId}`) history.pushState({pageId},'',`#${pageId}`);
    if(pageId==='page-training') setTimeout(()=>renderHistory(training?.history||{}),80);
    window.scrollTo({top:0,behavior:'smooth'});
  }
  document.querySelectorAll('.sidebar-link,.sidebar-brand,[data-go]').forEach(link=>link.addEventListener('click',event=>{
    event.preventDefault(); showPage(link.dataset.page||link.dataset.go);
  }));
  window.addEventListener('popstate',()=>showPage(location.hash.slice(1)||'page-overview',false));
  showPage(location.hash.slice(1)||'page-overview',false);

  function renderDataset(info, samples=[]) {
    dataset=info; $('dataset-summary').hidden=!info;
    if($('kpi-total-images')) $('kpi-total-images').textContent = info ? Number(info.total_images||0).toLocaleString('ar') : '—';
    if($('kpi-classes-count')) $('kpi-classes-count').textContent = info?.classes ? `${info.classes.length} فئات` : '—';
    if(!info) return;
    $('dataset-summary').innerHTML=`<div class="summary-tile"><span>الصور الصالحة</span><b>${Number(info.total_images||0).toLocaleString('ar')}</b></div><div class="summary-tile"><span>عدد الفئات</span><b>${info.classes?.length||0}</b></div><div class="summary-tile"><span>صور غير قابلة للقراءة</span><b>${info.unreadable_images||0}</b></div>`;
    $('sample-gallery').hidden=!samples.length;
    $('sample-gallery').innerHTML=samples.map((sample,index)=>`<button class="gallery-card" type="button" data-sample="${index}"><img src="${sample.data_url}" alt="عينة من ${escapeHtml(sample.label)}"><b>${escapeHtml(sample.label)}</b><small>${sample.is_test_sample?'صورة اختبار مقترحة':'عينة من المجموعة'}</small></button>`).join('');
    $('sample-gallery').querySelectorAll('[data-sample]').forEach(button=>button.addEventListener('click',()=>predictSample(Number(button.dataset.sample))));
    $('train-button').disabled=false;
  }

  function chartOptions(minZero=false) { return {responsive:true,maintainAspectRatio:true,animation:false,plugins:{legend:{labels:{color:'#c5d1cd',font:{family:'Cairo',size:10}}}},scales:{x:{ticks:{color:chartColors.muted},grid:{color:'#ffffff10'}},y:{beginAtZero:minZero,ticks:{color:chartColors.muted},grid:{color:'#ffffff10'}}}}; }
  function makeOrUpdateChart(existing, canvasId, datasets, labels) {
    if(!window.Chart) return existing;
    if(!existing) return new Chart($(canvasId),{type:'line',data:{labels,datasets},options:chartOptions(false)});
    existing.data.labels=labels;existing.data.datasets=datasets;existing.update('none');return existing;
  }
  function renderHistory(history={}) {
    const length=Math.max(history.loss?.length||0,history.val_loss?.length||0,history.accuracy?.length||0,history.val_accuracy?.length||0);
    const labels=Array.from({length},(_,i)=>`عصر ${i+1}`);
    const ds=(values,label,color)=>({label,data:values||[],borderColor:color,backgroundColor:color+'33',borderWidth:2,pointRadius:2,tension:.25,fill:false});
    lossChart=makeOrUpdateChart(lossChart,'loss-chart',[ds(history.loss,'تدريب',chartColors.mint),ds(history.val_loss,'تحقق',chartColors.amber)],labels);
    accuracyChart=makeOrUpdateChart(accuracyChart,'accuracy-chart',[ds(history.accuracy,'تدريب',chartColors.aqua),ds(history.val_accuracy,'تحقق',chartColors.amber)],labels);
  }
  function renderMetrics(data) {
    result=data; $('results-step').hidden=false;
    $('results-empty').hidden=true;
    if($('kpi-test-accuracy')) $('kpi-test-accuracy').textContent = data.accuracy ? (data.accuracy * 100).toFixed(1) + '%' : '—';
    const metrics=[['دقة الاختبار',data.accuracy],['عدد الصور',data.test_images],['العصور المنفذة',data.epochs_completed],['الصورة',`${data.image_size}×${data.image_size}`]];
    $('metrics').innerHTML=metrics.map(([name,value])=>`<div class="metric"><span>${name}</span><b>${typeof value==='number'&&name==='دقة الاختبار'?(value*100).toFixed(1)+'%':escapeHtml(value)}</b></div>`).join('');
    $('result-caption').textContent=`${data.architecture} · اختبار مستقل من ${data.test_images} صورة.`;
    const labels=data.labels||[],matrix=data.confusion_matrix||[],max=Math.max(1,...matrix.flat());
    $('confusion-matrix').innerHTML=labels.length?`<table><thead><tr><th>الحقيقي ↓ / المتوقع →</th>${labels.map(x=>`<th>${escapeHtml(x)}</th>`).join('')}</tr></thead><tbody>${matrix.map((row,i)=>`<tr><th>${escapeHtml(labels[i])}</th>${row.map(value=>`<td style="background:rgba(80,201,211,${(.08+.72*value/max).toFixed(2)})">${value}</td>`).join('')}</tr>`).join('')}</tbody></table>`:'—';
    const report=data.per_class||[];
    $('class-report').innerHTML=report.length?`<thead><tr><th>الفئة</th><th>Precision</th><th>Recall</th><th>F1</th><th>الدعم</th></tr></thead><tbody>${report.map(item=>`<tr><td>${escapeHtml(item.label)}</td><td>${(item.precision*100).toFixed(1)}%</td><td>${(item.recall*100).toFixed(1)}%</td><td>${(item.f1*100).toFixed(1)}%</td><td>${item.support}</td></tr>`).join('')}</tbody>`:'—';
    if(window.Chart){if(classChart)classChart.destroy();classChart=new Chart($('class-chart'),{type:'bar',data:{labels:report.map(x=>x.label),datasets:[{label:'Precision',data:report.map(x=>x.precision),backgroundColor:chartColors.aqua},{label:'Recall',data:report.map(x=>x.recall),backgroundColor:chartColors.mint},{label:'F1',data:report.map(x=>x.f1),backgroundColor:chartColors.amber}]},options:{...chartOptions(true),scales:{...chartOptions(true).scales,y:{beginAtZero:true,max:1,ticks:{color:chartColors.muted},grid:{color:'#ffffff10'}}}}});}
    $('predict-button').disabled=false;
  }
  function renderTraining(value) {
    training=value;const status=value.status||'idle';const isRunning=status==='running';
    $('training-progress').hidden=!isRunning;
    $('train-button').disabled=isRunning||!dataset;
    if($('kpi-model-status')) $('kpi-model-status').textContent = isRunning ? 'قيد التدريب' : status==='completed' ? 'مكتمل' : 'جاهز';
    const pct=value.total_epochs?Math.min(100,Math.round(100*value.current_epoch/value.total_epochs)):0;
    $('progress-bar').value=pct;$('training-percent').textContent=`${pct}%`;
    $('training-message').textContent=isRunning?`العصر ${value.current_epoch} من ${value.total_epochs}`:status==='completed'?'اكتمل التدريب':status==='failed'?'فشل التدريب':'جارٍ تجهيز النموذج';
    $('network-status').textContent=isRunning?`يتدرب · ${pct}%`:status==='completed'?'اكتمل':'جاهزة';

    const statsBar = $('training-stats-bar');
    if (statsBar) {
      statsBar.hidden = !isRunning && status !== 'completed';
      if ($('train-stat-epoch')) $('train-stat-epoch').textContent = `${value.current_epoch || 0} / ${value.total_epochs || 0}`;
      const h = value.history || {};
      if (h.loss?.length && $('train-stat-loss')) $('train-stat-loss').textContent = h.loss[h.loss.length - 1].toFixed(4);
      if (h.val_loss?.length && $('train-stat-valloss')) $('train-stat-valloss').textContent = h.val_loss[h.val_loss.length - 1].toFixed(4);
      if (h.val_accuracy?.length && $('train-stat-valacc')) $('train-stat-valacc').textContent = (h.val_accuracy[h.val_accuracy.length - 1] * 100).toFixed(1) + '%';
    }

    renderHistory(value.history||{});window.WasteNetworkView?.setTraining(isRunning,pct);
    if(status==='failed'&&value.error) showError(value.error);
  }
  async function loadState() {
    try { const state=await request('/api/state');setService(true,'خدمة المشروع متصلة');renderDataset(state.dataset,state.samples||[]);renderTraining(state.training||{});if(state.result){renderMetrics(state.result);renderHistory(state.training?.history||{});} }
    catch(error){setService(false,'تعذر الاتصال بالخدمة');showError(error.message);}
  }

  $('dataset-file').addEventListener('change',async event=>{
    const file=event.target.files?.[0];if(!file)return;clearError();$('train-button').disabled=true;
    try {const form=new FormData();form.append('file',file);const state=await request('/api/dataset/upload',{method:'POST',body:form});renderDataset(state.dataset,state.samples);$('results-step').hidden=true;$('results-empty').hidden=false;$('predict-button').disabled=true;setService(true,'تم تحميل مجموعة الصور');}
    catch(error){showError(error.message);$('train-button').disabled=false;}finally{event.target.value='';}
  });
  const dropzone=$('dropzone');
  ['dragenter','dragover'].forEach(name=>dropzone.addEventListener(name,event=>{event.preventDefault();dropzone.classList.add('dragover');}));
  ['dragleave','drop'].forEach(name=>dropzone.addEventListener(name,event=>{event.preventDefault();dropzone.classList.remove('dragover');}));
  dropzone.addEventListener('drop',event=>{const file=event.dataTransfer.files?.[0];if(file){const dt=new DataTransfer();dt.items.add(file);$('dataset-file').files=dt.files;$('dataset-file').dispatchEvent(new Event('change'));}});

  $('train-button').addEventListener('click',async()=>{
    if(!dataset){showError('ارفع مجموعة صور أولًا.');return;}clearError();$('train-button').disabled=true;$('training-progress').hidden=false;$('progress-bar').value=0;window.WasteNetworkView?.setTraining(true,0);
    const settings={epochs:Number($('epochs').value),image_size:Number($('image-size').value),batch_size:Number($('batch-size').value),filters:Number($('filters').value),dropout:Number($('dropout').value),learning_rate:Number($('learning-rate').value),test_percent:Number($('test-percent').value),validation_percent:Number($('validation-percent').value),augmentation:$('augmentation').checked};
    try {await request('/api/train',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(settings)});await poll();}
    catch(error){showError(error.message);$('train-button').disabled=false;$('training-progress').hidden=true;window.WasteNetworkView?.setTraining(false,0);}
  });
  async function poll(){if(polling)return;polling=true;try{while(true){const value=await request('/api/progress');renderTraining(value);if(value.status==='completed'){const state=await request('/api/state');if(state.result)renderMetrics(state.result);renderDataset(state.dataset,state.samples||[]);window.WasteNetworkView?.setTraining(false,100);break;}if(value.status==='failed'){window.WasteNetworkView?.setTraining(false,0);break;}await new Promise(resolve=>setTimeout(resolve,1000));}}catch(error){showError(error.message);window.WasteNetworkView?.setTraining(false,0);}finally{polling=false;}}
  $('prediction-file').addEventListener('change',event=>{const file=event.target.files?.[0];if(!file)return;$('prediction-name').textContent=file.name;if(predictionUrl)URL.revokeObjectURL(predictionUrl);predictionUrl=URL.createObjectURL(file);$('prediction-preview').src=predictionUrl;$('prediction-preview').hidden=false;$('predict-button').disabled=!result;});
  $('predict-button').addEventListener('click',async()=>{const file=$('prediction-file').files?.[0];if(file)await classify(file);});
  async function predictSample(index){try{const response=await fetch(`/api/sample-image/${index}`,{credentials:'same-origin'});if(!response.ok)throw new Error('تعذر تحميل صورة الاختبار.');const blob=await response.blob();const file=new File([blob],`test-sample-${index}.jpg`,{type:blob.type||'image/jpeg'});$('prediction-name').textContent=`عينة اختبار ${index+1}`;if(predictionUrl)URL.revokeObjectURL(predictionUrl);predictionUrl=URL.createObjectURL(blob);$('prediction-preview').src=predictionUrl;$('prediction-preview').hidden=false;$('predict-button').disabled=!result;if(result)await classify(file);}catch(error){showError(error.message);}}
  document.querySelectorAll('[data-preset]').forEach(btn=>{
    btn.addEventListener('click',()=>{
      document.querySelectorAll('[data-preset]').forEach(b=>b.classList.remove('active'));
      btn.classList.add('active');
      const p=btn.dataset.preset;
      if(p==='quick'){$('epochs').value='5';$('image-size').value='128';$('filters').value='16';}
      else if(p==='balanced'){$('epochs').value='12';$('image-size').value='160';$('filters').value='32';}
      else if(p==='deep'){$('epochs').value='20';$('image-size').value='224';$('filters').value='64';}
    });
  });

  const disposalTips = {
    'plastic': '⚠️ بلاستيك: يُغسل ويُجفف قبل إعادة التدوير، ويُحظر إلقاؤه في الطبيعة لتجنب ابتلاعه من الحيوانات والطيور البرية.',
    'metal': '⚙️ معدن: علب المشروبات والأغذية - يُضغط الحجم وتُطوى الحواف الحادة لتفادي جرح الحيوانات وعمال النظافة.',
    'glass': '🍶 زجاج: يُفصل بحذر في حاويات الزجاج دون تكسير لتجنب تناثر الشظايا المؤذية للكائنات الحية.',
    'paper': '📄 ورق: يجب الحفاظ عليه جافاً ونظيفاً لتسهيل إعادة تدويره وحماية الغابات والأشجار.',
    'cardboard': '📦 كرتون: يُفكك ويُسطح لتوفير المساحة وتسهيل جمعه وإعادة تصنيعه.',
    'trash': '🗑️ نفايات عامة: للمواد غير القابلة للتدوير - توضع في أكياس محكمة الغلق لمنع انتشار الروائح والتلوث.'
  };

  async function classify(file){
    try {
      clearError();
      const form=new FormData();
      form.append('file',file);
      const prediction=await request('/api/predict',{method:'POST',body:form});
      const normLabel=String(prediction.prediction||'').toLowerCase();
      const tip=disposalTips[normLabel]||'♻️ تصنيف نفايات: يُرجى فرز المادة وفق تعليمات النظافة البلدية لمنع التلوث.';
      $('prediction-result').innerHTML=`<b class="predicted-label">${escapeHtml(prediction.prediction)}</b><p>احتمال أعلى فئة ${(prediction.confidence*100).toFixed(1)}%</p><div class="prob-list">${prediction.probabilities.map(item=>`<div class="prob-row"><span>${escapeHtml(item.label)}</span><div class="bar"><i style="width:${Math.max(0,Math.min(100,item.probability*100))}%"></i></div><b>${(item.probability*100).toFixed(1)}%</b></div>`).join('')}</div><div class="disposal-tip">${escapeHtml(tip)}</div>`;
    } catch(error){
      showError(error.message);
    }
  }

  function initNetwork(){
    const host=$('network-scene');if(!host||!window.THREE){if(host)host.textContent='تعذر تحميل الرسم ثلاثي الأبعاد.';return;}
    const T=window.THREE,scene=new T.Scene();scene.background=new T.Color(0x0e1a21);scene.fog=new T.Fog(0x0e1a21,13,34);
    const camera=new T.PerspectiveCamera(47,1,.1,100);camera.position.set(0,1,12);
    const renderer=new T.WebGLRenderer({antialias:true,alpha:true});renderer.setPixelRatio(Math.min(devicePixelRatio||1,1.6));host.appendChild(renderer.domElement);
    const controls=T.OrbitControls?new T.OrbitControls(camera,renderer.domElement):null;if(controls){controls.enableDamping=true;controls.minDistance=7;controls.maxDistance=20;}
    scene.add(new T.AmbientLight(0xb9eddf,.9));const point=new T.PointLight(0x60dbb6,2.1,24);point.position.set(-3,4,6);scene.add(point);scene.add(new T.GridHelper(16,16,0x31585a,0x1e383b));
    const group=new T.Group();scene.add(group);const objects=[];
    const stages=[-4.2,-1.4,1.4,4.2],colors=[0x52c9d2,0x66d8ba,0x9288ed,0xf1b75c];
    function makeBox(x,y,z,w,h,d,color){const mesh=new T.Mesh(new T.BoxGeometry(w,h,d),new T.MeshStandardMaterial({color,emissive:color,emissiveIntensity:.08,transparent:true,opacity:.88,roughness:.38,metalness:.2}));mesh.position.set(x,y,z);group.add(mesh);objects.push(mesh);return mesh;}
    function makeLabel(txt,x){const canvas=document.createElement('canvas');canvas.width=400;canvas.height=80;const ctx=canvas.getContext('2d');ctx.font='bold 27px Cairo';ctx.textAlign='center';ctx.fillStyle='#d8e6df';ctx.fillText(txt,200,52);const sprite=new T.Sprite(new T.SpriteMaterial({map:new T.CanvasTexture(canvas),transparent:true,depthWrite:false}));sprite.scale.set(2.8,.56,1);sprite.position.set(x,-3.15,0);group.add(sprite);}
    const input=[];for(let y=0;y<4;y++)for(let x=0;x<4;x++)input.push(makeBox(stages[0]+(x-1.5)*.34,1.2-y*.34,0,.29,.29,.09,colors[0]));
    const conv=[];for(let i=0;i<6;i++)conv.push(makeBox(stages[1],(i-2.5)*.55,(i-2.5)*.17,1.05,1.05,.13,colors[1]));
    const deep=[];for(let i=0;i<5;i++){const mesh=new T.Mesh(new T.OctahedronGeometry(.48,1),new T.MeshStandardMaterial({color:colors[2],emissive:colors[2],emissiveIntensity:.1,transparent:true,opacity:.9}));mesh.position.set(stages[2],(i-2)*.66,(i-2)*.13);group.add(mesh);objects.push(mesh);deep.push(mesh);}
    const output=[];const labels=['كرتون','زجاج','معدن','ورق','بلاستيك','أخرى'];for(let i=0;i<labels.length;i++){const mesh=new T.Mesh(new T.CylinderGeometry(.12,.12,.3,18),new T.MeshStandardMaterial({color:colors[3],emissive:colors[3],emissiveIntensity:.1}));mesh.position.set(stages[3],(i-2.5)*.55,0);group.add(mesh);objects.push(mesh);output.push(mesh);}
    makeLabel('الصورة',stages[0]);makeLabel('خرائط Conv',stages[1]);makeLabel('سمات عميقة',stages[2]);makeLabel('الفئات',stages[3]);
    for(let i=0;i<conv.length;i++){const line=new T.Line(new T.BufferGeometry().setFromPoints([input[i*2].position,conv[i].position]),new T.LineBasicMaterial({color:0x4b9b91,transparent:true,opacity:.34}));group.add(line);}
    for(let a=0;a<conv.length;a++)for(let b=0;b<deep.length;b++){const line=new T.Line(new T.BufferGeometry().setFromPoints([conv[a].position,deep[b].position]),new T.LineBasicMaterial({color:0x4b9b91,transparent:true,opacity:.2}));group.add(line);}
    for(const left of deep)for(const right of output){const line=new T.Line(new T.BufferGeometry().setFromPoints([left.position,right.position]),new T.LineBasicMaterial({color:0x4b9b91,transparent:true,opacity:.12}));group.add(line);}
    let running=false,amount=0;
    function resize(){const w=Math.max(host.clientWidth,280),h=Math.max(host.clientHeight,210);camera.aspect=w/h;camera.updateProjectionMatrix();renderer.setSize(w,h);}
    new ResizeObserver(resize).observe(host);
    function animate(){requestAnimationFrame(animate);const t=performance.now()*.001;group.rotation.y=Math.sin(t*.28)*.025;objects.forEach((obj,i)=>{const factor=running ? 0.78 + 0.2 * (0.5 + 0.5 * Math.sin(t * 4 + i * 0.5)) : 1;obj.scale.setScalar(factor);if(running)obj.material.emissiveIntensity=.08+.3*(.5+.5*Math.sin(t*5+i*.37));});if(controls)controls.update();renderer.render(scene,camera);}
    resize();animate();return {setTraining(on,pct){running=on;amount=pct||0;}};
  }
  window.WasteNetworkView=initNetwork();

  let cameraStream = null, cameraTimer = null, cameraBusy = false;
  const video = $('camera-video'), hudCanvas = $('camera-hud-canvas');

  async function startCamera() {
    try {
      clearError();
      cameraStream = await navigator.mediaDevices.getUserMedia({ video: { width: { ideal: 640 }, height: { ideal: 480 }, facingMode: 'environment' } });
      video.srcObject = cameraStream;
      await video.play();
      $('btn-start-camera').disabled = true;
      $('btn-stop-camera').disabled = false;
      $('btn-snap-camera').disabled = false;
      $('live-pred-label').textContent = 'الكاميرا نشطة · وجّه العنصر داخل المربع';
      cameraTimer = setInterval(captureAndClassify, 450);
    } catch (err) {
      showError('تعذر الوصول إلى الكاميرا: ' + (err.message || err));
    }
  }

  function stopCamera() {
    if (cameraTimer) clearInterval(cameraTimer);
    cameraTimer = null;
    if (cameraStream) {
      cameraStream.getTracks().forEach(t => t.stop());
      cameraStream = null;
    }
    video.srcObject = null;
    $('btn-start-camera').disabled = false;
    $('btn-stop-camera').disabled = true;
    $('btn-snap-camera').disabled = true;
    $('live-pred-label').textContent = 'تم إيقاف الكاميرا';
    $('live-pred-conf').textContent = '—';
    const ctx = hudCanvas?.getContext('2d');
    if (ctx) ctx.clearRect(0, 0, hudCanvas.width, hudCanvas.height);
  }

  async function captureAndClassify() {
    if (!video || !video.videoWidth || cameraBusy || !result) return;
    cameraBusy = true;
    try {
      const offCanvas = document.createElement('canvas');
      offCanvas.width = 240;
      offCanvas.height = 240;
      const octx = offCanvas.getContext('2d');
      const minDim = Math.min(video.videoWidth, video.videoHeight);
      const sx = (video.videoWidth - minDim) / 2;
      const sy = (video.videoHeight - minDim) / 2;
      octx.drawImage(video, sx, sy, minDim, minDim, 0, 0, 240, 240);

      const base64 = offCanvas.toDataURL('image/jpeg', 0.85);
      const res = await request('/api/camera/classify-frame', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ image_base64: base64 })
      });

      const pred = String(res.prediction || '').toLowerCase();
      const conf = Number(res.confidence || 0);
      $('live-pred-label').textContent = `الفئة: ${res.prediction.toUpperCase()}`;
      $('live-pred-conf').textContent = `نسبة الثقة: ${(conf * 100).toFixed(1)}%`;
      $('live-pred-tip').textContent = disposalTips[pred] || '♻️ فرز النفايات';

      // Update HUD Canvas
      if (hudCanvas) {
        hudCanvas.width = video.videoWidth;
        hudCanvas.height = video.videoHeight;
        const hctx = hudCanvas.getContext('2d');
        hctx.clearRect(0, 0, hudCanvas.width, hudCanvas.height);
        const roi = minDim * 0.55;
        const rx = (hudCanvas.width - roi) / 2, ry = (hudCanvas.height - roi) / 2;
        hctx.strokeStyle = '#00e5a3';
        hctx.lineWidth = 3;
        hctx.strokeRect(rx, ry, roi, roi);
        hctx.font = 'bold 20px Cairo, sans-serif';
        hctx.fillStyle = '#00e5a3';
        hctx.fillText(`${res.prediction} (${(conf * 100).toFixed(0)}%)`, rx, Math.max(26, ry - 10));
      }
    } catch (e) {
      // Soft fail
    } finally {
      cameraBusy = false;
    }
  }

  $('btn-start-camera')?.addEventListener('click', startCamera);
  $('btn-stop-camera')?.addEventListener('click', stopCamera);
  $('btn-snap-camera')?.addEventListener('click', () => {
    captureAndClassify();
    showPage('page-predict');
  });

  // HALVO Mode Switcher (User Mode vs Admin Studio)
  $('btn-mode-user')?.addEventListener('click', () => {
    $('btn-mode-user').classList.add('active');
    $('btn-mode-admin').classList.remove('active');
    showPage('page-user-sorter');
  });

  $('btn-mode-admin')?.addEventListener('click', () => {
    $('btn-mode-admin').classList.add('active');
    $('btn-mode-user').classList.remove('active');
    showPage('page-overview');
  });

  // User Mode File Selector & Instant AI Sorting
  let userFile = null;
  $('user-image-file')?.addEventListener('change', (e) => {
    const f = e.target.files?.[0];
    if (!f) return;
    userFile = f;
    const url = URL.createObjectURL(f);
    const p = $('user-preview-img');
    if (p) {
      p.src = url;
      p.style.display = 'block';
    }
  });

  $('btn-user-classify')?.addEventListener('click', async () => {
    if (!userFile) {
      showError('يرجى اختيار صورة أولاً.');
      return;
    }
    try {
      const fd = new FormData();
      fd.append('file', userFile);
      const res = await request('/api/predict', { method: 'POST', body: fd });
      const pred = String(res.prediction || '').toLowerCase();
      const conf = Number(res.confidence || 0);

      if ($('user-detected-class')) $('user-detected-class').textContent = res.prediction.toUpperCase();
      if ($('user-detected-conf')) $('user-detected-conf').textContent = `نسبة الثقة: ${(conf * 100).toFixed(1)}%`;
      if ($('user-eco-pct')) $('user-eco-pct').textContent = `${(conf * 100).toFixed(0)}%`;

      const binGuides = {
        'paper': { bin: 'السلة الزرقاء 🟦 (أوراق وكرتون)', tip: 'حافظ على الورق جافاً ونظيفاً لتسهيل إعادة تدويره وحماية الأشجار.', rec: 'قابلة لإعادة التدوير 100%' },
        'cardboard': { bin: 'السلة الزرقاء 🟦 (كرتون وورق مقوى)', tip: 'اطوِ الصناديق الكرتونية لتقليل الحجم وإفساح المجال في حاوية التدوير.', rec: 'قابلة لإعادة التدوير 100%' },
        'plastic': { bin: 'السلة الصفراء 🟨 (بلاستيك ومعادن)', tip: 'اغسل العبوة البلاستيكية وجففها قبل إلقائها لتجنب تلوث الشحنة.', rec: 'قابلة لإعادة التدوير' },
        'metal': { bin: 'السلة الصفراء 🟨 (معادن وعلب الألمنيوم)', tip: 'علب الأطعمة والمشروبات - اطوِ الحواف الحادة لتفادي جرح عمال النظافة.', rec: 'قابلة لإعادة التدوير 100%' },
        'glass': { bin: 'السلة الخضراء 🟩 (زجاج وزجاجات)', tip: 'ضع الزجاج بحذر دون تكسيره لتفادي خطر الشظايا الحادة.', rec: 'قابلة لإعادة التدوير بالكامل' },
        'trash': { bin: 'السلة السوداء ⬛ (نفايات عامة غير قابلة للتدوير)', tip: 'نفايات عامة أو مختلطة - احرص على إغلاق الكيس بإحكام لمنع الروائح والتلوث.', rec: 'طمر أو معالجة حرارية' }
      };

      const guide = binGuides[pred] || { bin: 'السلة العامة', tip: 'يُرجى فرز المادة وفق تعليمات النظافة البلدية.', rec: 'معالجة بيئية' };
      if ($('user-bin-text')) $('user-bin-text').innerHTML = `<b>السلة المخصصة:</b> ${guide.bin}<br/><br/><b>إرشاد الفرز:</b> ${guide.tip}`;
      if ($('user-recycle-status')) $('user-recycle-status').textContent = guide.rec;
    } catch (err) {
      showError(err.message || 'تعذر تصنيف الصورة.');
    }
  });

  loadState();
})();
