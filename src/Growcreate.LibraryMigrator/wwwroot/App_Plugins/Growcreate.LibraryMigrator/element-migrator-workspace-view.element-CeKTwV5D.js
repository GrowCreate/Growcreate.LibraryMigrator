var T = (e) => {
  throw TypeError(e);
};
var x = (e, i, t) => i.has(e) || T("Cannot " + t);
var P = (e, i, t) => (x(e, i, "read from private field"), t ? t.call(e) : i.get(e)), M = (e, i, t) => i.has(e) ? T("Cannot add the same private member more than once") : i instanceof WeakSet ? i.add(e) : i.set(e, t), B = (e, i, t, s) => (x(e, i, "write to private field"), s ? s.call(e, t) : i.set(e, t), t), g = (e, i, t) => (x(e, i, "access private method"), t);
import { UmbElementMixin as V } from "@umbraco-cms/backoffice/element-api";
import { LitElement as X, html as r, nothing as l, css as Y, state as f, customElement as F } from "@umbraco-cms/backoffice/external/lit";
import { UMB_WORKSPACE_CONTEXT as H } from "@umbraco-cms/backoffice/workspace";
import { UmbControllerBase as K } from "@umbraco-cms/backoffice/class-api";
import { UMB_AUTH_CONTEXT as J } from "@umbraco-cms/backoffice/auth";
const S = "/umbraco/management/api/v1/growcreate-library-migrator";
var _, c, R, $, A;
class Q extends K {
  constructor(t) {
    super(t);
    M(this, c);
    M(this, _);
    this.consumeContext(J, (s) => {
      B(this, _, s);
    });
  }
  async isApplicable(t) {
    return (await g(this, c, $).call(this, `/applicable/${t}`)).applicable;
  }
  preview(t) {
    return g(this, c, $).call(this, `/preview/${t}`);
  }
  migrate(t) {
    return g(this, c, A).call(this, `/migrate/${t}`);
  }
  status(t) {
    return g(this, c, $).call(this, `/status/${t}`);
  }
  restore(t) {
    return g(this, c, A).call(this, `/restore/${t}`);
  }
}
_ = new WeakMap(), c = new WeakSet(), R = async function() {
  var t;
  return (t = P(this, _)) == null ? void 0 : t.getLatestToken();
}, $ = async function(t) {
  const s = await g(this, c, R).call(this), n = await fetch(`${S}${t}`, {
    credentials: "include",
    headers: {
      Accept: "application/json",
      ...s ? { Authorization: `Bearer ${s}` } : {}
    }
  });
  if (!n.ok) {
    const b = await n.text().catch(() => n.statusText);
    throw new Error(`${n.status}: ${b}`);
  }
  return n.json();
}, A = async function(t) {
  const s = await g(this, c, R).call(this), n = await fetch(`${S}${t}`, {
    method: "POST",
    credentials: "include",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
      ...s ? { Authorization: `Bearer ${s}` } : {}
    },
    body: "{}"
  });
  if (!n.ok) {
    const b = await n.text().catch(() => n.statusText);
    throw new Error(`${n.status}: ${b}`);
  }
  return n.json();
};
var Z = Object.defineProperty, q = Object.getOwnPropertyDescriptor, L = (e) => {
  throw TypeError(e);
}, p = (e, i, t, s) => {
  for (var n = s > 1 ? void 0 : s ? q(i, t) : i, b = e.length - 1, k; b >= 0; b--)
    (k = e[b]) && (n = (s ? k(i, t, n) : k(n)) || n);
  return s && n && Z(i, t, n), n;
}, C = (e, i, t) => i.has(e) || L("Cannot " + t), u = (e, i, t) => (C(e, i, "read from private field"), i.get(e)), E = (e, i, t) => i.has(e) ? L("Cannot add the same private member more than once") : i instanceof WeakSet ? i.add(e) : i.set(e, t), ee = (e, i, t, s) => (C(e, i, "write to private field"), i.set(e, t), t), o = (e, i, t) => (C(e, i, "access private method"), t), m, h, a, z, D, N, W, O, U, I, w, v, j, G, y;
let d = class extends V(X) {
  constructor() {
    super(...arguments), E(this, a), this._loading = !1, this._migrating = !1, this._restoring = !1, this._status = null, this._report = null, this._migrationResult = null, this._restoreResult = null, this._error = null, E(this, m, new Q(this)), E(this, h);
  }
  connectedCallback() {
    super.connectedCallback(), this.consumeContext(H, (e) => {
      e && this.observe(
        e.unique,
        (i) => {
          i && i !== u(this, h) && (ee(this, h, i), o(this, a, z).call(this, i));
        },
        "_unique"
      );
    });
  }
  render() {
    var e, i;
    return r`
      <uui-box headline="Library Migration">
        ${this._loading ? r`<div class="loading"><uui-loader></uui-loader><span>Analysing content…</span></div>` : this._error ? r`<uui-notice type="danger" .headline=${"Error"}>${this._error}</uui-notice>` : r`
                ${(e = this._status) != null && e.hasMigration ? o(this, a, W).call(this, this._status) : l}
                ${this._restoreResult ? o(this, a, O).call(this, this._restoreResult) : l}
                ${this._report && !((i = this._status) != null && i.hasMigration) ? o(this, a, U).call(this, this._report) : l}
              `}
      </uui-box>
    `;
  }
};
m = /* @__PURE__ */ new WeakMap();
h = /* @__PURE__ */ new WeakMap();
a = /* @__PURE__ */ new WeakSet();
z = async function(e) {
  this._loading = !0, this._error = null, this._report = null, this._status = null, this._migrationResult = null, this._restoreResult = null;
  try {
    const [i, t] = await Promise.all([
      u(this, m).status(e),
      u(this, m).preview(e)
    ]);
    this._status = i, this._report = t;
  } catch (i) {
    this._error = i instanceof Error ? i.message : String(i);
  } finally {
    this._loading = !1;
  }
};
D = async function() {
  if (u(this, h)) {
    this._migrating = !0, this._error = null, this._migrationResult = null;
    try {
      this._migrationResult = await u(this, m).migrate(u(this, h)), this._status = await u(this, m).status(u(this, h));
    } catch (e) {
      this._error = e instanceof Error ? e.message : String(e);
    } finally {
      this._migrating = !1;
    }
  }
};
N = async function() {
  if (u(this, h)) {
    this._restoring = !0, this._error = null, this._restoreResult = null;
    try {
      this._restoreResult = await u(this, m).restore(u(this, h)), this._status = await u(this, m).status(u(this, h));
    } catch (e) {
      this._error = e instanceof Error ? e.message : String(e);
    } finally {
      this._restoring = !1;
    }
  }
};
W = function(e) {
  const i = e.migratedAt ? new Date(e.migratedAt).toLocaleString() : "unknown date", t = e.state === "PartiallyMigrated";
  return r`
      <uui-notice
        type=${t ? "warning" : "positive"}
        .headline=${t ? "Migration partially applied" : "Migration already applied"}>
        <p>
          <strong>${e.elementCount}</strong> element(s) were migrated to the Library on
          <strong>${i}</strong>.
        </p>
        ${t ? r`<p>
              <strong>${e.errorCount}</strong> property/properties could not be converted during
              migration. Review the issues below.
            </p>` : l}
        <p>
          Use <strong>Restore</strong> to undo the migration: the original documents are recreated
          with their original keys, and converted picker data types, remapped references and
          historical versions are reverted.
        </p>
        <div class="restore-actions">
          <uui-button
            look="secondary"
            color="danger"
            label="Restore original documents"
            ?disabled=${this._restoring}
            @click=${o(this, a, N)}>
            ${this._restoring ? r`<uui-loader></uui-loader> Restoring…` : "Restore original documents"}
          </uui-button>
        </div>
      </uui-notice>
      ${t && e.errors.length > 0 ? o(this, a, y).call(this, e.errors) : l}
    `;
};
O = function(e) {
  return e.success ? r`
        <uui-notice type="positive" headline="Restore complete">
          <p>
            <strong>${e.elementsCreated}</strong> document(s) recreated. Refresh the content
            tree to see the restored content.
          </p>
          ${e.errors.length > 0 ? r`<p>Some properties could not be restored — see errors below.</p>` : l}
        </uui-notice>
        ${e.errors.length > 0 ? o(this, a, y).call(this, e.errors) : l}
      ` : r`
      <uui-notice type="danger" headline="Restore failed">
        <p>The restore did not complete. See errors below.</p>
      </uui-notice>
      ${o(this, a, y).call(this, e.errors)}
    `;
};
U = function(e) {
  if (e.blockers.length > 0)
    return r`
        <uui-notice type="danger" .headline=${`Migration blocked (${e.blockers.length})`}>
          <p>
            These documents have child nodes. Migrating them would delete the whole subtree, which
            cannot be restored. Move or remove their children first.
          </p>
          <ul class="warnings">
            ${e.blockers.map(
      (t) => r`<li><uui-icon name="icon-alert"></uui-icon>${t}</li>`
    )}
          </ul>
        </uui-notice>
      `;
  if (!e.migratable)
    return r`
        <uui-notice type="default">
          No eligible child content found on this page. Open a container whose direct children are
          documents of a single type suitable for migration to the Library.
        </uui-notice>
      `;
  const i = e.types.reduce((t, s) => t + s.documentCountSitewide, 0);
  return r`
      <p class="summary">
        Found <strong>${i}</strong> document(s) across <strong>${e.types.length}</strong>
        type(s) that can be migrated to the Library section. Review the details below before running
        the migration.
      </p>

      ${e.types.map((t) => o(this, a, I).call(this, t))}
      ${e.affectedPickers.length > 0 ? o(this, a, w).call(this, e.affectedPickers, "Document") : l}
      ${e.affectedBlockEditors.length > 0 ? o(this, a, v).call(this, e.affectedBlockEditors, "Document") : l}
      ${e.affectedMediaPickers.length > 0 ? o(this, a, w).call(this, e.affectedMediaPickers, "Media") : l}
      ${e.affectedMediaBlockEditors.length > 0 ? o(this, a, v).call(this, e.affectedMediaBlockEditors, "Media") : l}
      ${e.affectedMemberPickers.length > 0 ? o(this, a, w).call(this, e.affectedMemberPickers, "Member") : l}
      ${e.affectedMemberBlockEditors.length > 0 ? o(this, a, v).call(this, e.affectedMemberBlockEditors, "Member") : l}
      ${e.warnings.length > 0 ? o(this, a, j).call(this, e.warnings) : l}

      <div class="actions">
        <uui-button
          look="primary"
          color="danger"
          label="Run Migration"
          ?disabled=${this._migrating}
          @click=${o(this, a, D)}>
          ${this._migrating ? r`<uui-loader></uui-loader> Migrating…` : "Run Migration"}
        </uui-button>
        <p class="actions-note">
          <uui-icon name="icon-warning"></uui-icon>
          This will permanently delete all matching documents and recreate them as Library elements.
        </p>
      </div>

      ${this._migrationResult ? o(this, a, G).call(this, this._migrationResult) : l}
    `;
};
I = function(e) {
  return r`
      <uui-box class="type-box" .headline=${`${e.typeName}  ·  ${e.typeAlias}`}>
        <dl>
          <dt>Documents site-wide</dt>
          <dd>${e.documentCountSitewide}</dd>
          <dt>Direct children here</dt>
          <dd>${e.directChildCount}</dd>
          <dt>Already an element type</dt>
          <dd>
            <uui-tag .color=${e.isAlreadyElement ? "positive" : "default"}>
              ${e.isAlreadyElement ? "Yes" : "No"}
            </uui-tag>
          </dd>
          <dt>Already allowed in Library</dt>
          <dd>
            <uui-tag .color=${e.isAlreadyAllowedInLibrary ? "positive" : "default"}>
              ${e.isAlreadyAllowedInLibrary ? "Yes" : "No"}
            </uui-tag>
          </dd>
        </dl>
      </uui-box>
    `;
};
w = function(e, i) {
  return r`
      <uui-box .headline=${`${i} picker properties (${e.length} site-wide)`}>
        <p>
          These ContentPicker and MultiNodeTreePicker properties exist across your ${i.toLowerCase()}
          types. After migration, any that hold references to the migrated content will be converted to
          Element Pickers and their stored values remapped.
        </p>
        <uui-table>
          <uui-table-head>
            <uui-table-head-cell>${i} type</uui-table-head-cell>
            <uui-table-head-cell>Property</uui-table-head-cell>
            <uui-table-head-cell>Editor</uui-table-head-cell>
            <uui-table-head-cell>Data type</uui-table-head-cell>
          </uui-table-head>
          ${e.map(
    (t) => r`
              <uui-table-row>
                <uui-table-cell
                  >${t.ownerDocTypeName} <code>(${t.ownerDocTypeAlias})</code></uui-table-cell
                >
                <uui-table-cell
                  >${t.propertyName} <code>(${t.propertyAlias})</code></uui-table-cell
                >
                <uui-table-cell><code>${t.pickerEditorAlias}</code></uui-table-cell>
                <uui-table-cell>${t.dataTypeName}</uui-table-cell>
              </uui-table-row>
            `
  )}
        </uui-table>
      </uui-box>
    `;
};
v = function(e, i) {
  return r`
      <uui-box .headline=${`${i} Block List / Block Grid properties (${e.length} site-wide)`}>
        <p>
          These Block List and Block Grid properties exist across your ${i.toLowerCase()} types. After
          migration, any embedded ContentPicker references within block content data that point to the
          migrated content will have their stored UDIs remapped automatically.
        </p>
        <uui-table>
          <uui-table-head>
            <uui-table-head-cell>${i} type</uui-table-head-cell>
            <uui-table-head-cell>Property</uui-table-head-cell>
            <uui-table-head-cell>Editor</uui-table-head-cell>
          </uui-table-head>
          ${e.map(
    (t) => r`
              <uui-table-row>
                <uui-table-cell
                  >${t.ownerDocTypeName} <code>(${t.ownerDocTypeAlias})</code></uui-table-cell
                >
                <uui-table-cell
                  >${t.propertyName} <code>(${t.propertyAlias})</code></uui-table-cell
                >
                <uui-table-cell><code>${t.pickerEditorAlias}</code></uui-table-cell>
              </uui-table-row>
            `
  )}
        </uui-table>
      </uui-box>
    `;
};
j = function(e) {
  return r`
      <uui-box .headline=${`Warnings (${e.length})`}>
        <ul class="warnings">
          ${e.map(
    (i) => r`
              <li><uui-icon name="icon-alert"></uui-icon>${i}</li>
            `
  )}
        </ul>
      </uui-box>
    `;
};
G = function(e) {
  return e.success ? r`
        <uui-notice type="positive" headline="Migration complete">
          <p>
            <strong>${e.elementsCreated}</strong> element(s) created in the Library.
            Refresh the tree to see the new Library folder.
          </p>
          ${e.errors.length > 0 ? r`<p>Some picker properties could not be remapped — see errors below.</p>` : l}
        </uui-notice>
        ${e.errors.length > 0 ? o(this, a, y).call(this, e.errors) : l}
      ` : r`
      <uui-notice type="danger" headline="Migration failed">
        <p>The migration did not complete successfully. See errors below.</p>
      </uui-notice>
      ${o(this, a, y).call(this, e.errors)}
    `;
};
y = function(e) {
  return r`
      <uui-box .headline=${`Errors (${e.length})`}>
        <ul class="warnings">
          ${e.map(
    (i) => r`<li><uui-icon name="icon-alert"></uui-icon>${i}</li>`
  )}
        </ul>
      </uui-box>
    `;
};
d.styles = Y`
    :host {
      display: block;
      padding: var(--uui-size-space-5);
    }

    uui-box {
      margin-bottom: var(--uui-size-space-5);
    }

    .loading {
      display: flex;
      align-items: center;
      gap: var(--uui-size-space-3);
    }

    .summary {
      margin: 0 0 var(--uui-size-space-5);
    }

    .type-box dl {
      display: grid;
      grid-template-columns: max-content 1fr;
      gap: var(--uui-size-space-2) var(--uui-size-space-5);
      margin: 0;
    }

    .type-box dt {
      font-weight: 600;
      color: var(--uui-color-text);
    }

    .type-box dd {
      margin: 0;
    }

    uui-table {
      width: 100%;
    }

    code {
      font-family: monospace;
      font-size: 0.85em;
      background: var(--uui-color-surface-alt);
      padding: 0 3px;
      border-radius: 2px;
    }

    .warnings {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: var(--uui-size-space-3);
    }

    .warnings li {
      display: flex;
      align-items: flex-start;
      gap: var(--uui-size-space-2);
    }

    .warnings uui-icon {
      flex-shrink: 0;
      color: var(--uui-color-warning);
    }

    .actions {
      display: flex;
      flex-direction: column;
      gap: var(--uui-size-space-3);
      padding-top: var(--uui-size-space-4);
      border-top: 1px solid var(--uui-color-border);
    }

    .actions-note {
      display: flex;
      align-items: center;
      gap: var(--uui-size-space-2);
      margin: 0;
      font-size: 0.9em;
      color: var(--uui-color-text-alt);
    }

    .actions-note uui-icon {
      flex-shrink: 0;
    }

    .restore-actions {
      margin-top: var(--uui-size-space-3);
    }
  `;
p([
  f()
], d.prototype, "_loading", 2);
p([
  f()
], d.prototype, "_migrating", 2);
p([
  f()
], d.prototype, "_restoring", 2);
p([
  f()
], d.prototype, "_status", 2);
p([
  f()
], d.prototype, "_report", 2);
p([
  f()
], d.prototype, "_migrationResult", 2);
p([
  f()
], d.prototype, "_restoreResult", 2);
p([
  f()
], d.prototype, "_error", 2);
d = p([
  F("element-migrator-workspace-view")
], d);
const ne = d;
export {
  d as ElementMigratorWorkspaceViewElement,
  ne as default
};
