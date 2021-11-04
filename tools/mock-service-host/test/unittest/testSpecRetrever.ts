import 'reflect-metadata' // Must be imported exactly once for Inversify

import * as assert from 'assert'
import * as fs from 'fs-extra'
import * as lodash from 'lodash'
import * as path from 'path'
import { SpecRetrievalMethod } from '../../src/common/environment'
import { SpecRetrieverFilesystem } from '../../src/lib/specRetrieverFilesystem'
import { SpecRetrieverGit } from '../../src/lib/specRetrieverGit'
import { config } from '../../src/common/index'

describe('#SpecRetrieverGit tests by modify config', () => {
    const localPath = './cache'

    afterAll(() => {
        if (fs.pathExistsSync(localPath)) {
            fs.emptyDirSync(localPath)
        }
        return
    })

    it('it should return true when retrieve from file', async () => {
        const cloneConfig = lodash.cloneDeep(config)
        cloneConfig.specRetrievalMethod = SpecRetrievalMethod.Filesystem
        const specRetrieverFile: SpecRetrieverFilesystem = new SpecRetrieverFilesystem(cloneConfig)
        try {
            await specRetrieverFile.retrieveSpecs()
        } catch {
            assert.fail('File system retrieveSpecs should not fail')
        }
    })
    it('it should return true when retrieve from git', async () => {
        const cloneConfig = lodash.cloneDeep(config)
        cloneConfig.specRetrievalMethod = SpecRetrievalMethod.Git
        cloneConfig.specRetrievalGitAuthMode = 'none'
        cloneConfig.specRetrievalLocalRelativePath = localPath
        const specRetrieverGit: SpecRetrieverGit = new SpecRetrieverGit(cloneConfig)
        try {
            await specRetrieverGit.retrieveSpecs()
        } catch {
            assert.fail('Git retrieveSpecs should not fail')
        }
        assert.strictEqual(fs.pathExistsSync(path.join(localPath, '.git')), true)
    })
})
