import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { LitElement, html, css, nothing, customElement, state } from '@umbraco-cms/backoffice/external/lit';
import { UMB_WORKSPACE_CONTEXT } from '@umbraco-cms/backoffice/workspace';
import {
  ElementMigratorService,
  type MigrationResult,
  type MigrationStatus,
  type PreviewReport,
  type MigratableTypeReport,
  type PickerPropertyUsage,
} from '../api/element-migrator.service.js';

@customElement('element-migrator-workspace-view')
export class ElementMigratorWorkspaceViewElement extends UmbElementMixin(LitElement) {
  @state() private _loading = false;
  @state() private _migrating = false;
  @state() private _restoring = false;
  @state() private _status: MigrationStatus | null = null;
  @state() private _report: PreviewReport | null = null;
  @state() private _migrationResult: MigrationResult | null = null;
  @state() private _restoreResult: MigrationResult | null = null;
  @state() private _error: string | null = null;

  readonly #service = new ElementMigratorService(this);
  #currentKey: string | undefined;

  override connectedCallback() {
    super.connectedCallback();
    this.consumeContext(UMB_WORKSPACE_CONTEXT, (context) => {
      if (!context) return;
      this.observe(
        (context as any).unique,
        (unique: string | undefined) => {
          if (unique && unique !== this.#currentKey) {
            this.#currentKey = unique;
            this.#loadData(unique);
          }
        },
        '_unique',
      );
    });
  }

  async #loadData(documentKey: string) {
    this._loading = true;
    this._error = null;
    this._report = null;
    this._status = null;
    this._migrationResult = null;
    this._restoreResult = null;
    try {
      const [status, report] = await Promise.all([
        this.#service.status(documentKey),
        this.#service.preview(documentKey),
      ]);
      this._status = status;
      this._report = report;
    } catch (e) {
      this._error = e instanceof Error ? e.message : String(e);
    } finally {
      this._loading = false;
    }
  }

  async #runMigration() {
    if (!this.#currentKey) return;
    this._migrating = true;
    this._error = null;
    this._migrationResult = null;
    try {
      this._migrationResult = await this.#service.migrate(this.#currentKey);
      this._status = await this.#service.status(this.#currentKey);
    } catch (e) {
      this._error = e instanceof Error ? e.message : String(e);
    } finally {
      this._migrating = false;
    }
  }

  async #runRestore() {
    if (!this.#currentKey) return;
    this._restoring = true;
    this._error = null;
    this._restoreResult = null;
    try {
      this._restoreResult = await this.#service.restore(this.#currentKey);
      this._status = await this.#service.status(this.#currentKey);
    } catch (e) {
      this._error = e instanceof Error ? e.message : String(e);
    } finally {
      this._restoring = false;
    }
  }

  override render() {
    return html`
      <uui-box headline="Library Migration">
        ${this._loading
          ? html`<div class="loading"><uui-loader></uui-loader><span>Analysing content…</span></div>`
          : this._error
            ? html`<uui-notice type="danger" .headline=${'Error'}>${this._error}</uui-notice>`
            : html`
                ${this._status?.hasMigration ? this.#renderMigratedBanner(this._status) : nothing}
                ${this._restoreResult ? this.#renderRestoreResult(this._restoreResult) : nothing}
                ${this._report && !this._status?.hasMigration ? this.#renderReport(this._report) : nothing}
              `}
      </uui-box>
    `;
  }

  #renderMigratedBanner(status: MigrationStatus) {
    const dateStr = status.migratedAt
      ? new Date(status.migratedAt).toLocaleString()
      : 'unknown date';
    const partial = status.state === 'PartiallyMigrated';
    return html`
      <uui-notice
        type=${partial ? 'warning' : 'positive'}
        .headline=${partial ? 'Migration partially applied' : 'Migration already applied'}>
        <p>
          <strong>${status.elementCount}</strong> element(s) were migrated to the Library on
          <strong>${dateStr}</strong>.
        </p>
        ${partial
          ? html`<p>
              <strong>${status.errorCount}</strong> property/properties could not be converted during
              migration. Review the issues below.
            </p>`
          : nothing}
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
            @click=${this.#runRestore}>
            ${this._restoring ? html`<uui-loader></uui-loader> Restoring…` : 'Restore original documents'}
          </uui-button>
        </div>
      </uui-notice>
      ${partial && status.errors.length > 0 ? this.#renderErrors(status.errors) : nothing}
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
          ${result.errors.length > 0 ? html`<p>Some properties could not be restored — see errors below.</p>` : nothing}
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

  #renderReport(report: PreviewReport) {
    if (report.blockers.length > 0) {
      return html`
        <uui-notice type="danger" .headline=${`Migration blocked (${report.blockers.length})`}>
          <p>
            These documents have child nodes. Migrating them would delete the whole subtree, which
            cannot be restored. Move or remove their children first.
          </p>
          <ul class="warnings">
            ${report.blockers.map(
              (b) => html`<li><uui-icon name="icon-alert"></uui-icon>${b}</li>`,
            )}
          </ul>
        </uui-notice>
      `;
    }

    if (!report.migratable) {
      return html`
        <uui-notice type="default">
          No eligible child content found on this page. Open a container whose direct children are
          documents of a single type suitable for migration to the Library.
        </uui-notice>
      `;
    }

    const totalDocs = report.types.reduce((sum, t) => sum + t.documentCountSitewide, 0);

    return html`
      <p class="summary">
        Found <strong>${totalDocs}</strong> document(s) across <strong>${report.types.length}</strong>
        type(s) that can be migrated to the Library section. Review the details below before running
        the migration.
      </p>

      ${report.types.map((t) => this.#renderTypeReport(t))}
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
          This will permanently delete all matching documents and recreate them as Library elements.
        </p>
      </div>

      ${this._migrationResult ? this.#renderMigrationResult(this._migrationResult) : nothing}
    `;
  }

  #renderTypeReport(type: MigratableTypeReport) {
    return html`
      <uui-box class="type-box" .headline=${`${type.typeName}  ·  ${type.typeAlias}`}>
        <dl>
          <dt>Documents site-wide</dt>
          <dd>${type.documentCountSitewide}</dd>
          <dt>Direct children here</dt>
          <dd>${type.directChildCount}</dd>
          <dt>Already an element type</dt>
          <dd>
            <uui-tag .color=${type.isAlreadyElement ? 'positive' : 'default'}>
              ${type.isAlreadyElement ? 'Yes' : 'No'}
            </uui-tag>
          </dd>
          <dt>Already allowed in Library</dt>
          <dd>
            <uui-tag .color=${type.isAlreadyAllowedInLibrary ? 'positive' : 'default'}>
              ${type.isAlreadyAllowedInLibrary ? 'Yes' : 'No'}
            </uui-tag>
          </dd>
        </dl>
      </uui-box>
    `;
  }

  #renderPickers(pickers: PickerPropertyUsage[], scope: string) {
    return html`
      <uui-box .headline=${`${scope} picker properties (${pickers.length} site-wide)`}>
        <p>
          These ContentPicker and MultiNodeTreePicker properties exist across your ${scope.toLowerCase()}
          types. After migration, any that hold references to the migrated content will be converted to
          Element Pickers and their stored values remapped.
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
                <uui-table-cell
                  >${p.ownerDocTypeName} <code>(${p.ownerDocTypeAlias})</code></uui-table-cell
                >
                <uui-table-cell
                  >${p.propertyName} <code>(${p.propertyAlias})</code></uui-table-cell
                >
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
          These Block List and Block Grid properties exist across your ${scope.toLowerCase()} types. After
          migration, any embedded ContentPicker references within block content data that point to the
          migrated content will have their stored UDIs remapped automatically.
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
                <uui-table-cell
                  >${e.ownerDocTypeName} <code>(${e.ownerDocTypeAlias})</code></uui-table-cell
                >
                <uui-table-cell
                  >${e.propertyName} <code>(${e.propertyAlias})</code></uui-table-cell
                >
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
          ${warnings.map(
            (w) => html`
              <li><uui-icon name="icon-alert"></uui-icon>${w}</li>
            `,
          )}
        </ul>
      </uui-box>
    `;
  }

  #renderMigrationResult(result: MigrationResult) {
    if (result.success) {
      return html`
        <uui-notice type="positive" headline="Migration complete">
          <p>
            <strong>${result.elementsCreated}</strong> element(s) created in the Library.
            Refresh the tree to see the new Library folder.
          </p>
          ${result.errors.length > 0
            ? html`<p>Some picker properties could not be remapped — see errors below.</p>`
            : nothing}
        </uui-notice>
        ${result.errors.length > 0 ? this.#renderErrors(result.errors) : nothing}
      `;
    }

    return html`
      <uui-notice type="danger" headline="Migration failed">
        <p>The migration did not complete successfully. See errors below.</p>
      </uui-notice>
      ${this.#renderErrors(result.errors)}
    `;
  }

  #renderErrors(errors: string[]) {
    return html`
      <uui-box .headline=${`Errors (${errors.length})`}>
        <ul class="warnings">
          ${errors.map(
            (e) => html`<li><uui-icon name="icon-alert"></uui-icon>${e}</li>`,
          )}
        </ul>
      </uui-box>
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
}

declare global {
  interface HTMLElementTagNameMap {
    'element-migrator-workspace-view': ElementMigratorWorkspaceViewElement;
  }
}

export default ElementMigratorWorkspaceViewElement;
