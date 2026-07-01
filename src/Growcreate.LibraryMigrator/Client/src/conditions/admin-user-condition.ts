import { UmbConditionBase } from '@umbraco-cms/backoffice/extension-registry';
import type { UmbConditionConfigBase } from '@umbraco-cms/backoffice/extension-api';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UMB_CURRENT_USER_CONTEXT } from '@umbraco-cms/backoffice/current-user';

type ConditionArgs = {
  config: UmbConditionConfigBase;
  onChange: (permitted: boolean) => void;
};

// Permits the extension only for admin users. The API also enforces admin access;
// this condition keeps the dashboard hidden from non-admins in the UI.
export class AdminUserCondition extends UmbConditionBase<UmbConditionConfigBase> {
  constructor(host: UmbControllerHost, args: ConditionArgs) {
    super(host, args);

    this.consumeContext(UMB_CURRENT_USER_CONTEXT, (ctx) => {
      if (!ctx) return;
      this.observe(
        ctx.isAdmin,
        (isAdmin) => {
          this.permitted = isAdmin ?? false;
        },
        '_isAdmin',
      );
    });
  }
}
