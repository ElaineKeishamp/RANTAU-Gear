import assert from 'node:assert/strict';
import { createHandler } from '../supabase/functions/staff-admin/handler.mjs';
const actor='11111111-1111-1111-1111-111111111111', target='22222222-2222-2222-2222-222222222222', requestId='33333333-3333-3333-3333-333333333333';
let passed=0;
function check(ok,label){assert.ok(ok,label); passed++; process.stdout.write(`PASS: ${label}\n`);}
const json=(status,body)=>new Response(JSON.stringify(body),{status});
function fixture(options={}){
 const state={creates:0,resets:0,finishes:0,authCreated:!!options.existing,completed:!!options.completed,rpcs:[],bodies:[]};
 const runtime={env:key=>key==='SUPABASE_URL'?'https://fixture.test':'FIXTURE_SERVICE_KEY',fetch:async(url,init)=>{
  const path=new URL(url).pathname;
  const body=init.body?JSON.parse(init.body):null;
  state.bodies.push({path,body});
  if(path==='/auth/v1/user') return json(options.invalidAuth?401:200,{id:actor,user_metadata:{role:'super_admin'}});
  if(path==='/rest/v1/rpc/staff_admin_prepare'){
   state.rpcs.push(body);
   if(options.denied) return json(403,{code:'42501',message:'Access denied'});
   return json(200,{done:state.completed,user_id:state.authCreated||body.p_action==='password'?target:null});
  }
  if(path==='/auth/v1/admin/users'){
   state.creates++;
   assert.equal(body.email_confirm,true);
   assert.equal(body.app_metadata.rantau_created_by,actor);
   assert.equal(body.app_metadata.rantau_creation_request,requestId);
   assert.equal(init.headers.Authorization,'Bearer FIXTURE_SERVICE_KEY');
   if(options.authError) return json(422,{message:'DO_NOT_LEAK '+body.password});
   state.authCreated=true;return json(200,{id:target});
  }
  if(path===`/auth/v1/admin/users/${target}`){state.resets++; assert.equal(init.method,'PUT'); return json(200,{id:target});}
  if(path==='/rest/v1/rpc/staff_admin_finish'){
   state.rpcs.push(body); state.finishes++;
   if(options.dropFinishOnce&&state.finishes===1) throw new Error('Simulated network loss');
   state.completed=true; return json(200,{user_id:target});
  }
  throw new Error('Unexpected path '+path);
 }};
 const handler=createHandler(runtime);
 const input={request_id:requestId,action:'create',email:'staff@example.test',display_name:'Staff fixture',role:'front_office',password:'TEST_ONLY_PASSWORD'};
 const call=(value=input,token='Bearer TEST_ACCESS')=>handler(new Request('https://fixture.test/functions/v1/staff-admin',{method:'POST',headers:token?{Authorization:token}:{},body:typeof value==='string'?value:JSON.stringify(value)}));
 return {state,input,call,handler};
}
{
 const f=fixture();check((await f.call(f.input,null)).status===401,'Anonymous requests denied before any admin action');check(f.state.creates===0,'Anonymous request creates no account');
 check((await f.handler(new Request('https://fixture.test',{method:'GET'}))).status===405,'Only POST accepted');
 check((await f.call('{bad')).status===400,'Malformed JSON rejected');
 check((await f.call({...f.input,password:'short'})).status===400,'Short password rejected');
}
{
 const f=fixture({invalidAuth:true});check((await f.call()).status===401&&f.state.creates===0,'Invalid token cannot create staff');
}
{
 const f=fixture({denied:true});check((await f.call()).status===403&&f.state.creates===0,'User metadata role cannot bypass database admin authorization');
}
{
 const f=fixture();const response=await f.call();check(response.status===200&&(await response.json()).user_id===target,'Authenticated admin creates account and profile');
 check(f.state.creates===1&&f.state.finishes===1,'Create and finish performed once');
 check(f.state.rpcs.every(body=>!JSON.stringify(body).includes('TEST_ONLY_PASSWORD')&&!JSON.stringify(body).includes('password"')),'Password never sent to RPC ledger');
 check(f.state.bodies.every(x=>!x.path.includes('invite')&&!x.path.includes('recover')),'No invitation or password email sent');
 const retry=await f.call({...f.input,password:'IGNORED_ON_COMPLETED_RETRY'});check(retry.status===200&&f.state.creates===1,'Completed request retry does not recreate Auth user');
}
{
 const f=fixture({dropFinishOnce:true});check((await f.call()).status===503,'Unknown result reported conservatively');
 check((await f.call()).status===200&&f.state.creates===1,'Retry recovers Auth commit without duplicate create');
}
{
 const f=fixture({authError:true});const response=await f.call();check(response.status===400&&!((await response.text()).includes('TEST_ONLY_PASSWORD')),'Auth error response never echoes submitted password');
}
{
 const f=fixture({existing:true});const response=await f.call({...f.input,action:'password',target_id:target});
 check(response.status===200&&f.state.resets===1&&f.state.creates===0,'Password reset targets existing Auth user');
 check(f.state.rpcs.every(body=>!JSON.stringify(body).includes('TEST_ONLY_PASSWORD')),'Reset password never enters database operation payload');
}
console.log(`${passed} Edge checks passed.`);
