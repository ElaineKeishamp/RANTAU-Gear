import { createHandler } from "./handler.mjs";
Deno.serve(createHandler({ fetch, env: (key: string) => Deno.env.get(key) }));
