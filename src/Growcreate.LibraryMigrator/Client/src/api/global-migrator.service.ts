import { UmbControllerBase } from '@umbraco-cms/backoffice/class-api';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import type {
  PickerPropertyUsage,
  MigrationResult,
  MigrationState,
} from './element-migrator.service.js';

const API_BASE = '/umbraco/management/api/v1/growcreate-library-migrator';

export interface EligibleDocType {
  key: string;
  alias: string;
  name: string;
  instanceCount: number;
  isAlreadyElement: boolean;
  isAlreadyAllowedInLibrary: boolean;
}

export interface GlobalPreviewReport {
  docTypeKey: string;
  docTypeAlias: string;
  docTypeName: string;
  migratable: boolean;
  instanceCount: number;
  distinctParentCount: number;
  isAlreadyElement: boolean;
  isAlreadyAllowedInLibrary: boolean;
  affectedPickers: PickerPropertyUsage[];
  affectedBlockEditors: PickerPropertyUsage[];
  affectedMediaPickers: PickerPropertyUsage[];
  affectedMediaBlockEditors: PickerPropertyUsage[];
  affectedMemberPickers: PickerPropertyUsage[];
  affectedMemberBlockEditors: PickerPropertyUsage[];
  warnings: string[];
  blockers: string[];
}

export interface GlobalMigrationRun {
  runId: string;
  docTypeKey: string;
  docTypeAlias: string;
  docTypeName: string;
  migratedAt: string | null;
  elementCount: number;
  errorCount: number;
  state: MigrationState;
  errors: string[];
}

export class GlobalMigratorService extends UmbControllerBase {
  #authContext: typeof UMB_AUTH_CONTEXT.TYPE | undefined;

  constructor(host: UmbControllerHost) {
    super(host);
    this.consumeContext(UMB_AUTH_CONTEXT, (context) => {
      this.#authContext = context;
    });
  }

  async #getToken(): Promise<string | undefined> {
    return this.#authContext?.getLatestToken();
  }

  async #get<T>(path: string): Promise<T> {
    const token = await this.#getToken();
    const response = await fetch(`${API_BASE}${path}`, {
      credentials: 'include',
      headers: {
        Accept: 'application/json',
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
    });

    if (!response.ok) {
      const text = await response.text().catch(() => response.statusText);
      throw new Error(`${response.status}: ${text}`);
    }

    return response.json() as Promise<T>;
  }

  async #post<T>(path: string): Promise<T> {
    const token = await this.#getToken();
    const response = await fetch(`${API_BASE}${path}`, {
      method: 'POST',
      credentials: 'include',
      headers: {
        Accept: 'application/json',
        'Content-Type': 'application/json',
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: '{}',
    });

    if (!response.ok) {
      const text = await response.text().catch(() => response.statusText);
      throw new Error(`${response.status}: ${text}`);
    }

    return response.json() as Promise<T>;
  }

  listEligibleDocTypes(): Promise<EligibleDocType[]> {
    return this.#get<EligibleDocType[]>('/eligible-types');
  }

  previewByDocType(docTypeKey: string): Promise<GlobalPreviewReport> {
    return this.#get<GlobalPreviewReport>(`/preview-type/${docTypeKey}`);
  }

  migrateByDocType(docTypeKey: string): Promise<GlobalMigrationRun> {
    return this.#post<GlobalMigrationRun>(`/migrate-type/${docTypeKey}`);
  }

  listRuns(): Promise<GlobalMigrationRun[]> {
    return this.#get<GlobalMigrationRun[]>('/runs');
  }

  restoreRun(runId: string): Promise<MigrationResult> {
    return this.#post<MigrationResult>(`/restore-run/${runId}`);
  }
}
