import type { ManifestWorkspaceView } from '@umbraco-cms/backoffice/extension-registry';
import { ContainerTypeCondition } from './conditions/container-type-condition.js';

const containerTypeCondition = {
  type: 'condition',
  alias: 'Growcreate.LibraryMigrator.Condition.ContainerType',
  name: 'Growcreate Library Migrator Container Type Condition',
  api: ContainerTypeCondition,
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

export const manifests = [containerTypeCondition, workspaceView];
