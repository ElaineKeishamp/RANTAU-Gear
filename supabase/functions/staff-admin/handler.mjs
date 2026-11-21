const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const roles = new Set(["super_admin", "front_office", "warehouse_qc"]);
const known = new Set(["RG_STAFF_INPUT", "RG_STAFF_EMAIL", "RG_STAFF_REQUEST", "RG_STAFF_ACCESS", "RG_STAFF_PENDING"]);
export function createHandler(runtime) {
  const reply = (status, body) => new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json", "Cache-Control": "no-store" } });
  return async (req) => {
    if (req.method !== "POST") return reply(405, { code: "RG_STAFF_METHOD" });
    const auth = req.headers.get("Authorization");
    if (!auth || !/^Bearer \S+$/i.test(auth)) return reply(401, { code: "RG_STAFF_AUTH" });
    const url = runtime.env("SUPABASE_URL");
    const key = runtime.env("SUPABASE_SERVICE_ROLE_KEY");
    if (!url || !key) return reply(503, { code: "RG_STAFF_SERVER" });
    const call = async (path, body, token, method = "POST") => {
      const response = await runtime.fetch(`${url}${path}`, { method, headers: { apikey: key, Authorization: token, "Content-Type": "application/json" }, ...(method === "GET" ? {} : { body: JSON.stringify(body) }), signal: AbortSignal.timeout(20000) });
      const data = await response.json();
      return { response, data };
    };
    const rpc = (name, body, token = auth) => call(`/rest/v1/rpc/${name}`, body, token);
    const failure = ({ response, data }) => reply(response.status >= 500 ? 503 : response.status === 401 ? 401 : response.status === 403 || data?.code === "42501" ? 403 : 400, { code: known.has(data?.message) ? data.message : "RG_STAFF_SERVER" });
    try {
      // Verify identity with Auth; never authorize from decoded claims or user_metadata.
      const identity = await call("/auth/v1/user", null, auth, "GET");
      if (!identity.response.ok || !uuid.test(identity.data?.id ?? "")) return reply(401, { code: "RG_STAFF_AUTH" });
      const text = await req.text();
      if (text.length > 8192) return reply(413, { code: "RG_STAFF_INPUT" });
      let input;
      try { input = JSON.parse(text); } catch { return reply(400, { code: "RG_STAFF_INPUT" }); }
      if (!uuid.test(input.request_id ?? "") || !["create", "password"].includes(input.action) || typeof input.password !== "string" || input.password.length < 8 || input.password.length > 128) return reply(400, { code: "RG_STAFF_INPUT" });
      if (input.action === "create" && (typeof input.email !== "string" || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(input.email) || input.email.length > 254 || typeof input.display_name !== "string" || !input.display_name.trim() || input.display_name.length > 150 || !roles.has(input.role))) return reply(400, { code: "RG_STAFF_INPUT" });
      if (input.action === "password" && !uuid.test(input.target_id ?? "")) return reply(400, { code: "RG_STAFF_INPUT" });
      const values = input.action === "create" ? { email: input.email.trim().toLowerCase(), display_name: input.display_name.trim(), role: input.role } : {};
      const parameters = { p_request_id: input.request_id, p_action: input.action, p_target_id: input.action === "password" ? input.target_id : null, p_values: values };
      let preparation = await rpc("staff_admin_prepare", parameters);
      if (!preparation.response.ok) return failure(preparation);
      if (preparation.data.done) return reply(200, { user_id: preparation.data.user_id });
      const service = `Bearer ${key}`;
      if (input.action === "create" && !preparation.data.user_id) {
        const created = await call("/auth/v1/admin/users", { email: values.email, password: input.password, email_confirm: true, app_metadata: { rantau_creation_request: input.request_id, rantau_created_by: identity.data.id } }, service);
        if (!created.response.ok) {
          if (created.response.status >= 500) return reply(503, { code: "RG_STAFF_UNCERTAIN" });
          // Another invocation may have committed Auth before its response was lost.
          preparation = await rpc("staff_admin_prepare", parameters);
          if (!preparation.response.ok) return failure(preparation);
          if (!preparation.data.user_id) return reply(400, { code: "RG_STAFF_AUTH_INPUT" });
        }
      } else if (input.action === "password") {
        const changed = await call(`/auth/v1/admin/users/${input.target_id}`, { password: input.password }, service, "PUT");
        if (!changed.response.ok) return reply(changed.response.status >= 500 ? 503 : 400, { code: changed.response.status >= 500 ? "RG_STAFF_UNCERTAIN" : "RG_STAFF_AUTH_INPUT" });
      }
      const finish = await rpc("staff_admin_finish", { p_request_id: input.request_id, p_actor: identity.data.id }, service);
      if (!finish.response.ok) return reply(503, { code: "RG_STAFF_UNCERTAIN" });
      return reply(200, { user_id: finish.data.user_id });
    } catch {
      // Do not log exception objects, request bodies, passwords or upstream response text.
      return reply(503, { code: "RG_STAFF_UNCERTAIN" });
    }
  };
}
