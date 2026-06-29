import { umbExtensionsRegistry } from '@umbraco-cms/backoffice/extension-registry';
import { manifests } from './manifests.js';

umbExtensionsRegistry.registerMany(manifests);
