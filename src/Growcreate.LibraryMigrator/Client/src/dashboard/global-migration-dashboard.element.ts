import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { LitElement, html, css, nothing, customElement, state } from '@umbraco-cms/backoffice/external/lit';
import {
  GlobalMigratorService,
  type EligibleDocType,
  type GlobalMigrationRun,
  type GlobalPreviewReport,
} from '../api/global-migrator.service.js';
import type { MigrationResult, PickerPropertyUsage } from '../api/element-migrator.service.js';

@customElement('gc-global-migration-dashboard')
export class GlobalMigrationDashboardElement extends UmbElementMixin(LitElement) {
  @state() private _loadingTypes = false;
  @state() private _loadingPreview = false;
  @state() private _migrating = false;
  @state() private _restoringRunId: string | null = null;

  @state() private _eligibleTypes: EligibleDocType[] = [];
  @state() private _selectedType: EligibleDocType | null = null;
  @state() private _preview: GlobalPreviewReport | null = null;
  @state() private _lastRun: GlobalMigrationRun | null = null;
  @state() private _runs: GlobalMigrationRun[] = [];
  @state() private _lastRestore: MigrationResult | null = null;
  @state() private _error: string | null = null;

  readonly #service = new GlobalMigratorService(this);

  override connectedCallback() {
    super.connectedCallback();
    this.#loadTypes();
    this.#loadRuns();
  }

  async #loadTypes() {
    this._loadingTypes = true;
    this._error = null;
    try {
      this._eligibleTypes = await this.#service.listEligibleDocTypes();
    } catch (e) {
      this._error = e instanceof Error ? e.message : String(e);
    } finally {
      this._loadingTypes = false;
    }
  }

  async #loadRuns() {
    try {
      this._runs = await this.#service.listRuns();
    } catch {
      // non-fatal
    }
  }

  async #selectType(type: EligibleDocType) {
    this._selectedType = type;
    this._preview = null;
    this._lastRun = null;
    this._lastRestore = null;
    this._loadingPreview = true;
    this._error = null;
    try {
      this._preview = await this.#service.previewByDocType(type.key);
    } catch (e) {
      this._error = e instanceof Error ? e.message : String(e);
    } finally {
      this._loadingPreview = false;
    }
  }

  #clearSelection() {
    this._selectedType = null;
    this._preview = null;
    this._lastRun = null;
    this._lastRestore = null;
    this._error = null;
  }

  async #runMigration() {
    if (!this._selectedType) return;
    this._migrating = true;
    this._error = null;
    this._lastRun = null;
    try {
      this._lastRun = await this.#service.migrateByDocType(this._selectedType.key);
      await this.#loadRuns();
      await this.#loadTypes();
    } catch (e) {
      this._error = e instanceof Error ? e.message : String(e);
    } finally {
      this._migrating = false;
    }
  }

  async #restoreRun(runId: string) {
    this._restoringRunId = runId;
    this._error = null;
    this._lastRestore = null;
    try {
      this._lastRestore = await this.#service.restoreRun(runId);
      await this.#loadRuns();
      await this.#loadTypes();
    } catch (e) {
      this._error = e instanceof Error ? e.message : String(e);
    } finally {
      this._restoringRunId = null;
    }
  }

  override render() {
    return html`
      <uui-box headline="Global Library Migration">
        <p class="intro">
          Migrate every instance of a document type into the Umbraco 18 Library in one run. Only
          document types with no template and at least one existing instance are shown.
        </p>
        ${this._error ? html`<uui-notice type="danger" headline="Error">${this._error}</uui-notice>` : nothing}
        ${this._selectedType ? this.#renderSelectedView() : this.#renderTypeList()}
      </uui-box>
      ${this.#renderRunsBox()}
    `;
  }

  #renderTypeList() {
    if (this._loadingTypes) {
      return html`<div class="loading"><uui-loader></uui-loader><span>Loading eligible document types…</span></div>`;
    }
    if (this._eligibleTypes.length === 0) {
      return html`
        <uui-notice type="default">
          No eligible document types found. A document type must have no template assigned and at
          least one instance in the content tree to appear here.
        </uui-notice>
      `;
    }
    return html`
      <uui-table>
        <uui-table-head>
          <uui-table-head-cell>Name</uui-table-head-cell>
          <uui-table-head-cell>Alias</uui-table-head-cell>
          <uui-table-head-cell>Instances</uui-table-head-cell>
          <uui-table-head-cell>Element</uui-table-head-cell>
          <uui-table-head-cell>Allowed in Library</uui-table-head-cell>
          <uui-table-head-cell></uui-table-head-cell>
        </uui-table-head>
        ${this._eligibleTypes.map(
          (t) => html`
            <uui-table-row>
              <uui-table-cell>${t.name}</uui-table-cell>
              <uui-table-cell><code>${t.alias}</code></uui-table-cell>
              <uui-table-cell>${t.instanceCount}</uui-table-cell>
              <uui-table-cell>
                <uui-tag .color=${t.isAlreadyElement ? 'positive' : 'default'}>
                  ${t.isAlreadyElement ? 'Yes' : 'No'}
                </uui-tag>
              </uui-table-cell>
              <uui-table-cell>
                <uui-tag .color=${t.isAlreadyAllowedInLibrary ? 'positive' : 'default'}>
                  ${t.isAlreadyAllowedInLibrary ? 'Yes' : 'No'}
                </uui-tag>
              </uui-table-cell>
              <uui-table-cell>
                <uui-button
                  look="secondary"
                  label="Analyse"
                  @click=${() => this.#selectType(t)}>
                  Analyse
                </uui-button>
              </uui-table-cell>
            </uui-table-row>
          `,
        )}
      </uui-table>
    `;
  }

  #renderSelectedView() {
    const type = this._selectedType!;
    const hasCompletedRun =
      this._lastRun !== null && this._lastRun.state !== 'NotMigrated';

    return html`
      <div class="selection-header">
        <div>
          <h3>${type.name} <code>(${type.alias})</code></h3>
        </div>
        <uui-button look="secondary" label="Back to list" @click=${this.#clearSelection}>
          ← Back to list
        </uui-button>
      </div>
      ${hasCompletedRun
        ? this.#renderRunResult(this._lastRun!)
        : this._loadingPreview
          ? html`<div class="loading"><uui-loader></uui-loader><span>Analysing content…</span></div>`
          : html`
              ${this._preview ? this.#renderPreview(this._preview) : nothing}
              ${this._lastRun ? this.#renderRunResult(this._lastRun) : nothing}
            `}
    `;
  }

  #renderPreview(report: GlobalPreviewReport) {
    if (report.blockers.length > 0) {
      return html`
        <uui-notice type="danger" .headline=${`Migration blocked (${report.blockers.length})`}>
          <p>
            These documents have child nodes. Migrating them would delete the whole subtree, which
            cannot be restored. Move or remove their children first.
          </p>
          <ul class="warnings">
            ${report.blockers.map((b) => html`<li><uui-icon name="icon-alert"></uui-icon>${b}</li>`)}
          </ul>
        </uui-notice>
      `;
    }

    if (!report.migratable || report.instanceCount === 0) {
      return html`
        <uui-notice type="default">
          No migratable instances of this type were found.
        </uui-notice>
      `;
    }

    return html`
      <p class="summary">
        Found <strong>${report.instanceCount}</strong> document(s) of type
        <strong>${report.docTypeName}</strong> across
        <strong>${report.distinctParentCount}</strong> distinct parent(s). Each parent will get its
        own Library folder.
      </p>
      <uui-box class="type-box" .headline=${'Document type status'}>
        <dl>
          <dt>Already an element type</dt>
          <dd>
            <uui-tag .color=${report.isAlreadyElement ? 'positive' : 'default'}>
              ${report.isAlreadyElement ? 'Yes' : 'No'}
            </uui-tag>
          </dd>
          <dt>Already allowed in Library</dt>
          <dd>
            <uui-tag .color=${report.isAlreadyAllowedInLibrary ? 'positive' : 'default'}>
              ${report.isAlreadyAllowedInLibrary ? 'Yes' : 'No'}
            </uui-tag>
          </dd>
        </dl>
      </uui-box>
      ${report.affectedPickers.length > 0 ? this.#renderPickers(report.affectedPickers, 'Document') : nothing}
      ${report.affectedBlockEditors.length > 0 ? this.#renderBlockEditors(report.affectedBlockEditors, 'Document') : nothing}
      ${report.affectedMediaPickers.length > 0 ? this.#renderPickers(report.affectedMediaPickers, 'Media') : nothing}
      ${report.affectedMediaBlockEditors.length > 0 ? this.#renderBlockEditors(report.affectedMediaBlockEditors, 'Media') : nothing}
      ${report.affectedMemberPickers.length > 0 ? this.#renderPickers(report.affectedMemberPickers, 'Member') : nothing}
      ${report.affectedMemberBlockEditors.length > 0 ? this.#renderBlockEditors(report.affectedMemberBlockEditors, 'Member') : nothing}
      ${report.warnings.length > 0 ? this.#renderWarnings(report.warnings) : nothing}
      <div class="actions">
        <uui-button
          look="primary"
          color="danger"
          label="Run Migration"
          ?disabled=${this._migrating}
          @click=${this.#runMigration}>
          ${this._migrating ? html`<uui-loader></uui-loader> Migrating…` : 'Run Migration'}
        </uui-button>
        <p class="actions-note">
          <uui-icon name="icon-warning"></uui-icon>
          This will permanently delete all matching documents and recreate them as Library elements
          grouped by their immediate parent.
        </p>
      </div>
    `;
  }

  #renderPickers(pickers: PickerPropertyUsage[], scope: string) {
    return html`
      <uui-box .headline=${`${scope} picker properties (${pickers.length} site-wide)`}>
        <p>
          These ContentPicker and MultiNodeTreePicker properties exist across your ${scope.toLowerCase()}
          types. Any that hold references to the migrated content will be converted to Element Pickers
          and their stored values remapped.
        </p>
        <uui-table>
          <uui-table-head>
            <uui-table-head-cell>${scope} type</uui-table-head-cell>
            <uui-table-head-cell>Property</uui-table-head-cell>
            <uui-table-head-cell>Editor</uui-table-head-cell>
            <uui-table-head-cell>Data type</uui-table-head-cell>
          </uui-table-head>
          ${pickers.map(
            (p) => html`
              <uui-table-row>
                <uui-table-cell>${p.ownerDocTypeName} <code>(${p.ownerDocTypeAlias})</code></uui-table-cell>
                <uui-table-cell>${p.propertyName} <code>(${p.propertyAlias})</code></uui-table-cell>
                <uui-table-cell><code>${p.pickerEditorAlias}</code></uui-table-cell>
                <uui-table-cell>${p.dataTypeName}</uui-table-cell>
              </uui-table-row>
            `,
          )}
        </uui-table>
      </uui-box>
    `;
  }

  #renderBlockEditors(editors: PickerPropertyUsage[], scope: string) {
    return html`
      <uui-box .headline=${`${scope} Block List / Block Grid properties (${editors.length} site-wide)`}>
        <p>
          Embedded ContentPicker references within these block properties will have their stored UDIs
          remapped automatically where they point at the migrated content.
        </p>
        <uui-table>
          <uui-table-head>
            <uui-table-head-cell>${scope} type</uui-table-head-cell>
            <uui-table-head-cell>Property</uui-table-head-cell>
            <uui-table-head-cell>Editor</uui-table-head-cell>
          </uui-table-head>
          ${editors.map(
            (e) => html`
              <uui-table-row>
                <uui-table-cell>${e.ownerDocTypeName} <code>(${e.ownerDocTypeAlias})</code></uui-table-cell>
                <uui-table-cell>${e.propertyName} <code>(${e.propertyAlias})</code></uui-table-cell>
                <uui-table-cell><code>${e.pickerEditorAlias}</code></uui-table-cell>
              </uui-table-row>
            `,
          )}
        </uui-table>
      </uui-box>
    `;
  }

  #renderWarnings(warnings: string[]) {
    return html`
      <uui-box .headline=${`Warnings (${warnings.length})`}>
        <ul class="warnings">
          ${warnings.map((w) => html`<li><uui-icon name="icon-alert"></uui-icon>${w}</li>`)}
        </ul>
      </uui-box>
    `;
  }

  #renderRunResult(run: GlobalMigrationRun) {
    const partial = run.state === 'PartiallyMigrated';
    const failed = run.state === 'NotMigrated';
    if (failed) {
      return html`
        <uui-notice type="danger" headline="Migration did not run">
          <p>${run.errors[0] ?? 'Migration failed.'}</p>
        </uui-notice>
      `;
    }
    return html`
      <uui-notice
        type=${partial ? 'warning' : 'positive'}
        .headline=${partial ? 'Migration partially applied' : 'Migration complete'}>
        <p>
          <strong>${run.elementCount}</strong> element(s) created in the Library. Refresh the tree to
          see the new folders.
        </p>
        ${run.errorCount > 0
          ? html`<p><strong>${run.errorCount}</strong> issue(s) reported — see below.</p>`
          : nothing}
      </uui-notice>
      ${run.errors.length > 0 ? this.#renderErrors(run.errors) : nothing}
    `;
  }

  #renderErrors(errors: string[]) {
    return html`
      <uui-box .headline=${`Errors (${errors.length})`}>
        <ul class="warnings">
          ${errors.map((e) => html`<li><uui-icon name="icon-alert"></uui-icon>${e}</li>`)}
        </ul>
      </uui-box>
    `;
  }

  #renderRunsBox() {
    if (this._runs.length === 0 && !this._lastRestore) return nothing;
    return html`
      <uui-box headline="Past global migration runs">
        ${this._lastRestore ? this.#renderRestoreResult(this._lastRestore) : nothing}
        ${this._runs.length === 0
          ? html`<p>No past runs.</p>`
          : html`
              <uui-table>
                <uui-table-head>
                  <uui-table-head-cell>Document type</uui-table-head-cell>
                  <uui-table-head-cell>Migrated at</uui-table-head-cell>
                  <uui-table-head-cell>Elements</uui-table-head-cell>
                  <uui-table-head-cell>State</uui-table-head-cell>
                  <uui-table-head-cell></uui-table-head-cell>
                </uui-table-head>
                ${this._runs.map(
                  (r) => html`
                    <uui-table-row>
                      <uui-table-cell>${r.docTypeName} <code>(${r.docTypeAlias})</code></uui-table-cell>
                      <uui-table-cell>
                        ${r.migratedAt ? new Date(r.migratedAt).toLocaleString() : '—'}
                      </uui-table-cell>
                      <uui-table-cell>${r.elementCount}</uui-table-cell>
                      <uui-table-cell>
                        <uui-tag .color=${r.state === 'Migrated' ? 'positive' : 'warning'}>
                          ${r.state}
                        </uui-tag>
                      </uui-table-cell>
                      <uui-table-cell>
                        <uui-button
                          look="secondary"
                          color="danger"
                          label="Restore"
                          ?disabled=${this._restoringRunId !== null}
                          @click=${() => this.#restoreRun(r.runId)}>
                          ${this._restoringRunId === r.runId
                            ? html`<uui-loader></uui-loader> Restoring…`
                            : 'Restore'}
                        </uui-button>
                      </uui-table-cell>
                    </uui-table-row>
                  `,
                )}
              </uui-table>
            `}
      </uui-box>
    `;
  }

  #renderRestoreResult(result: MigrationResult) {
    if (result.success) {
      return html`
        <uui-notice type="positive" headline="Restore complete">
          <p>
            <strong>${result.elementsCreated}</strong> document(s) recreated. Refresh the content
            tree to see the restored content.
          </p>
        </uui-notice>
        ${result.errors.length > 0 ? this.#renderErrors(result.errors) : nothing}
      `;
    }
    return html`
      <uui-notice type="danger" headline="Restore failed">
        <p>The restore did not complete. See errors below.</p>
      </uui-notice>
      ${this.#renderErrors(result.errors)}
    `;
  }

  static override styles = css`
    :host {
      display: block;
      padding: var(--uui-size-space-5);
    }

    uui-box {
      margin-bottom: var(--uui-size-space-5);
    }

    .intro {
      margin: 0 0 var(--uui-size-space-4);
      color: var(--uui-color-text-alt);
    }

    .loading {
      display: flex;
      align-items: center;
      gap: var(--uui-size-space-3);
      padding: var(--uui-size-space-4);
    }

    .selection-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: var(--uui-size-space-4);
      gap: var(--uui-size-space-4);
    }

    .selection-header h3 {
      margin: 0;
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
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'gc-global-migration-dashboard': GlobalMigrationDashboardElement;
  }
}

export default GlobalMigrationDashboardElement;
