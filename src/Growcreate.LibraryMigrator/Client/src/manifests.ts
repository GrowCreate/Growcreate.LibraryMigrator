import type { ManifestWorkspaceView, ManifestDashboard } from '@umbraco-cms/backoffice/extension-registry';
import { ContainerTypeCondition } from './conditions/container-type-condition.js';
import { AdminUserCondition } from './conditions/admin-user-condition.js';

const containerTypeCondition = {
  type: 'condition',
  alias: 'Growcreate.LibraryMigrator.Condition.ContainerType',
  name: 'Growcreate Library Migrator Container Type Condition',
  api: ContainerTypeCondition,
};

const adminUserCondition = {
  type: 'condition',
  alias: 'Growcreate.LibraryMigrator.Condition.AdminUser',
  name: 'Growcreate Library Migrator Admin User Condition',
  api: AdminUserCondition,
};

const workspaceView: ManifestWorkspaceView = {
  type: 'workspaceView',
  alias: 'Growcreate.LibraryMigrator.WorkspaceView',
  name: 'Growcreate Library Migrator Workspace View',
  element: () => import('./workspace-view/element-migrator-workspace-view.element.js'),
  weight: -100,
  meta: {
    label: 'Library Migration',
    pathname: 'library-migration',
    icon: 'icon-shuffle',
  },
  conditions: [
    { alias: 'Umb.Condition.WorkspaceAlias', match: 'Umb.Workspace.Document' },
    { alias: 'Growcreate.LibraryMigrator.Condition.ContainerType' },
  ],
};

const dashboard: ManifestDashboard = {
  type: 'dashboard',
  alias: 'Growcreate.LibraryMigrator.Dashboard',
  name: 'Growcreate Library Migrator Dashboard',
  element: () => import('./dashboard/library-migrator-dashboard.element.js'),
  weight: -100,
  meta: {
    label: 'Library Migration',
    pathname: 'library-migration',
  },
  conditions: [
    { alias: 'Umb.Condition.SectionAlias', match: 'Umb.Section.Content' },
    { alias: 'Growcreate.LibraryMigrator.Condition.AdminUser' },
  ],
};

export const manifests = [containerTypeCondition, adminUserCondition, workspaceView, dashboard];
