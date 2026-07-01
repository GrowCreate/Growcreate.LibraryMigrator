import type { ManifestWorkspaceView } from '@umbraco-cms/backoffice/extension-registry';
import { ContainerTypeCondition } from './conditions/container-type-condition.js';
import { AdminCondition } from './conditions/admin-condition.js';

const containerTypeCondition = {
  type: 'condition',
  alias: 'Growcreate.LibraryMigrator.Condition.ContainerType',
  name: 'Growcreate Library Migrator Container Type Condition',
  api: ContainerTypeCondition,
};

const adminCondition = {
  type: 'condition',
  alias: 'Growcreate.LibraryMigrator.Condition.Admin',
  name: 'Growcreate Library Migrator Admin Condition',
  api: AdminCondition,
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

const globalMigrationDashboard = {
  type: 'dashboard',
  alias: 'Growcreate.LibraryMigrator.Dashboard.GlobalMigration',
  name: 'Growcreate Library Migrator Global Migration Dashboard',
  element: () => import('./dashboard/global-migration-dashboard.element.js'),
  weight: 1000,
  meta: {
    label: 'Library Migration',
    pathname: 'library-migration',
  },
  conditions: [
    { alias: 'Umb.Condition.SectionAlias', match: 'Umb.Section.Content' },
    { alias: 'Growcreate.LibraryMigrator.Condition.Admin' },
  ],
};

export const manifests = [
  containerTypeCondition,
  adminCondition,
  workspaceView,
  globalMigrationDashboard,
];
