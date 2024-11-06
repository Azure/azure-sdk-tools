// import { AutoRestOptions } from '@ts-common/azure-js-dev-tools';
// import { Configuration, MessageEmitter } from '@microsoft.azure/autorest-core/dist/lib/configuration';
// import { RealFileSystem } from '@microsoft.azure/autorest-core/dist/lib/file-system';

export const resolveChangedTags = async (
): Promise<string[]> => {
  throw new Error('tags-changed-in-batch is not supported yet.');

  // Comment out for not supported yet
  // const config = new Configuration(new RealFileSystem(), `file://${readmeMdFileUrl}`);
  // const more = new Array<{}>();
  // for (const each of Object.keys(autorestOptions)) {
  //   if (autorestOptions[each] === '') {
  //     more.push({ 'try-require' : `readme.${each}.md` });
  //     autorestOptions[each] = true;
  //   }
  // }

  // const configView = await config.CreateView(new MessageEmitter(), true, autorestOptions, ...more);
  // const batchConfig = configView.batch;
  // if (!(batchConfig instanceof Array)) {
  //   throw new Error('"tags-changed-in-batch" was used but cannot find "batch" section in readme');
  // }

  // return batchConfig.map(configLine => configLine.tag as string);
};
