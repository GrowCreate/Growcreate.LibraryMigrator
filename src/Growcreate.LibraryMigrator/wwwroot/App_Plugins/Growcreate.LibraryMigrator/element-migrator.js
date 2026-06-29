var y = (t) => {
  throw TypeError(t);
};
var C = (t, i, e) => i.has(t) || y("Cannot " + e);
var s = (t, i, e) => (C(t, i, "read from private field"), e ? e.call(t) : i.get(t)), h = (t, i, e) => i.has(t) ? y("Cannot add the same private member more than once") : i instanceof WeakSet ? i.add(t) : i.set(t, e), o = (t, i, e, p) => (C(t, i, "write to private field"), p ? p.call(t, e) : i.set(t, e), e);
import { UmbConditionBase as u, umbExtensionsRegistry as w } from "@umbraco-cms/backoffice/extension-registry";
import { UMB_WORKSPACE_CONTEXT as T } from "@umbraco-cms/backoffice/workspace";
import { UMB_AUTH_CONTEXT as g } from "@umbraco-cms/backoffice/auth";
import { UMB_CURRENT_USER_CONTEXT as A } from "@umbraco-cms/backoffice/current-user";
const M = "/umbraco/management/api/v1/growcreate-library-migrator";
var c, n, a;
class _ extends u {
  constructor(e, p) {
    super(e, p);
    h(this, c);
    h(this, n, !1);
    h(this, a, !1);
    this.consumeContext(g, (r) => {
      o(this, c, r);
    }), this.consumeContext(A, (r) => {
      r && this.observe(
        r.isAdmin,
        (m) => {
          o(this, n, m ?? !1), this.permitted = s(this, n) && s(this, a);
        },
        "_isAdmin"
      );
    }), this.consumeContext(T, (r) => {
      r && this.observe(
        r.unique,
        async (m) => {
          var l;
          if (!m) {
            o(this, a, !1), this.permitted = !1;
            return;
          }
          try {
            const d = await ((l = s(this, c)) == null ? void 0 : l.getLatestToken()), f = await fetch(`${M}/applicable/${m}`, {
              credentials: "include",
              headers: {
                Accept: "application/json",
                ...d ? { Authorization: `Bearer ${d}` } : {}
              }
            });
            if (!f.ok) {
              o(this, a, !1), this.permitted = !1;
              return;
            }
            const b = await f.json();
            o(this, a, b.applicable), this.permitted = s(this, n) && s(this, a);
          } catch {
            o(this, a, !1), this.permitted = !1;
          }
        },
        "_containerType"
      );
    });
  }
}
c = new WeakMap(), n = new WeakMap(), a = new WeakMap();
const k = {
  type: "condition",
  alias: "Growcreate.LibraryMigrator.Condition.ContainerType",
  name: "Growcreate Library Migrator Container Type Condition",
  api: _
}, U = {
  type: "workspaceView",
  alias: "Growcreate.LibraryMigrator.WorkspaceView",
  name: "Growcreate Library Migrator Workspace View",
  element: () => import("./element-migrator-workspace-view.element-CeKTwV5D.js"),
  weight: -100,
  meta: {
    label: "Library Migration",
    pathname: "library-migration",
    icon: "icon-shuffle"
  },
  conditions: [
    { alias: "Umb.Condition.WorkspaceAlias", match: "Umb.Workspace.Document" },
    { alias: "Growcreate.LibraryMigrator.Condition.ContainerType" }
  ]
}, E = [k, U];
w.registerMany(E);
