import { UmbConditionBase } from '@umbraco-cms/backoffice/extension-registry';
import type { UmbConditionConfigBase } from '@umbraco-cms/backoffice/extension-api';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UMB_WORKSPACE_CONTEXT } from '@umbraco-cms/backoffice/workspace';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { UMB_CURRENT_USER_CONTEXT } from '@umbraco-cms/backoffice/current-user';

const API_BASE = '/umbraco/management/api/v1/growcreate-library-migrator';

type ConditionArgs = {
  config: UmbConditionConfigBase;
  onChange: (permitted: boolean) => void;
};

export class ContainerTypeCondition extends UmbConditionBase<UmbConditionConfigBase> {
  #authContext: typeof UMB_AUTH_CONTEXT.TYPE | undefined;
  #isAdmin = false;
  #applicable = false;

  constructor(host: UmbControllerHost, args: ConditionArgs) {
    super(host, args);

    this.consumeContext(UMB_AUTH_CONTEXT, (ctx) => {
      this.#authContext = ctx;
    });

    this.consumeContext(UMB_CURRENT_USER_CONTEXT, (ctx) => {
      if (!ctx) return;
      this.observe(
        ctx.isAdmin,
        (isAdmin) => {
          this.#isAdmin = isAdmin ?? false;
          this.permitted = this.#isAdmin && this.#applicable;
        },
        '_isAdmin',
      );
    });

    this.consumeContext(UMB_WORKSPACE_CONTEXT, (context) => {
      if (!context) return;
      this.observe(
        (context as any).unique,
        async (unique: string | undefined) => {
          if (!unique) {
            this.#applicable = false;
            this.permitted = false;
            return;
          }
          try {
            const token = await this.#authContext?.getLatestToken();
            const response = await fetch(`${API_BASE}/applicable/${unique}`, {
              credentials: 'include',
              headers: {
                Accept: 'application/json',
                ...(token ? { Authorization: `Bearer ${token}` } : {}),
              },
            });
            if (!response.ok) {
              this.#applicable = false;
              this.permitted = false;
              return;
            }
            const data = (await response.json()) as { applicable: boolean };
            this.#applicable = data.applicable;
            this.permitted = this.#isAdmin && this.#applicable;
          } catch {
            this.#applicable = false;
            this.permitted = false;
          }
        },
        '_containerType',
      );
    });
  }
}
