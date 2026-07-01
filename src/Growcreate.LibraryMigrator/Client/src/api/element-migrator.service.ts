import { UmbControllerBase } from '@umbraco-cms/backoffice/class-api';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';

const API_BASE = '/umbraco/management/api/v1/growcreate-library-migrator';

export interface MigratableTypeReport {
  typeAlias: string;
  typeName: string;
  typeKey: string;
  isAlreadyElement: boolean;
  isAlreadyAllowedInLibrary: boolean;
  documentCountSitewide: number;
  directChildCount: number;
}

export interface PickerPropertyUsage {
  propertyAlias: string;
  propertyName: string;
  ownerDocTypeAlias: string;
  ownerDocTypeName: string;
  pickerEditorAlias: string;
  dataTypeName: string;
}

export interface PreviewReport {
  containerName: string;
  containerKey: string;
  migratable: boolean;
  types: MigratableTypeReport[];
  affectedPickers: PickerPropertyUsage[];
  affectedBlockEditors: PickerPropertyUsage[];
  affectedMediaPickers: PickerPropertyUsage[];
  affectedMediaBlockEditors: PickerPropertyUsage[];
  affectedMemberPickers: PickerPropertyUsage[];
  affectedMemberBlockEditors: PickerPropertyUsage[];
  warnings: string[];
  blockers: string[];
}

export interface EligibleType {
  typeKey: string;
  typeAlias: string;
  typeName: string;
  documentCountSitewide: number;
}

export interface MigrationResult {
  success: boolean;
  elementsCreated: number;
  libraryFolderKeys: string[];
  errors: string[];
}

export type MigrationState = 'NotMigrated' | 'Migrated' | 'PartiallyMigrated';

export interface MigrationStatus {
  hasMigration: boolean;
  state: MigrationState;
  migratedAt: string | null;
  elementCount: number;
  errorCount: number;
  errors: string[];
}

export class ElementMigratorService extends UmbControllerBase {
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

  async isApplicable(documentKey: string): Promise<boolean> {
    const data = await this.#get<{ applicable: boolean }>(`/applicable/${documentKey}`);
    return data.applicable;
  }

  preview(documentKey: string): Promise<PreviewReport> {
    return this.#get<PreviewReport>(`/preview/${documentKey}`);
  }

  migrate(documentKey: string): Promise<MigrationResult> {
    return this.#post<MigrationResult>(`/migrate/${documentKey}`);
  }

  status(documentKey: string): Promise<MigrationStatus> {
    return this.#get<MigrationStatus>(`/status/${documentKey}`);
  }

  restore(documentKey: string): Promise<MigrationResult> {
    return this.#post<MigrationResult>(`/restore/${documentKey}`);
  }

  listEligibleTypes(): Promise<EligibleType[]> {
    return this.#get<EligibleType[]>(`/types`);
  }

  previewType(typeKey: string): Promise<PreviewReport> {
    return this.#get<PreviewReport>(`/types/${typeKey}/preview`);
  }

  migrateType(typeKey: string): Promise<MigrationResult> {
    return this.#post<MigrationResult>(`/types/${typeKey}/migrate`);
  }

  statusType(typeKey: string): Promise<MigrationStatus> {
    return this.#get<MigrationStatus>(`/types/${typeKey}/status`);
  }

  restoreType(typeKey: string): Promise<MigrationResult> {
    return this.#post<MigrationResult>(`/types/${typeKey}/restore`);
  }
}
