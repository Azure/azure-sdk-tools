import * as fs from 'fs-extra'
import * as path from 'path'
import simpleGit, { SimpleGit } from 'simple-git'

import { BaseSpecRetriever, SpecRetriever } from './specRetriever'
import { Config } from '../common/config'
import { InjectableTypes } from './injectableTypes'
import { SpecRetrievalAuthMode } from '../common/environment'
import { inject, injectable } from 'inversify'
import { logger } from '../common/utils'

/**
 * Retrieves specs using git and caches them locally.
 */
@injectable()
export class SpecRetrieverGit extends BaseSpecRetriever implements SpecRetriever {
    constructor(@inject(InjectableTypes.Config) config: Config) {
        super(config)
    }

    protected async retrieveSpecsImpl(): Promise<void> {
        const url = new URL(this.config.specRetrievalGitUrl)
        logger.info(
            `SpecRetrieverGit: Retrieving specs from git` +
                `, protocol ${url.protocol}` +
                `, hostname ${url.hostname}` +
                `, pathname ${url.pathname}` +
                `, branch ${this.config.specRetrievalGitBranch}` +
                `, commitid ${this.config.specRetrievalGitCommitID}` +
                `, auth mode ${this.config.specRetrievalGitAuthMode}`
        )

        const startTime = Date.now()

        if (this.config.specRetrievalGitAuthMode === SpecRetrievalAuthMode.Token) {
            await this.addAuthToUrlForAuthModeToken(url)
        }
        const urlWithAuth = url.toString()

        const git: SimpleGit = simpleGit(this.localPath)
        const gitOptions = ['--depth=1', `--branch=${this.config.specRetrievalGitBranch}`]

        if (this.existGitRepo()) {
            // Use silent(true) to avoid any auth information being sent to stdout
            if (this.config.specRetrievalGitAuthMode === SpecRetrievalAuthMode.Token) {
                await git.silent(true).pull()
            }
        } else {
            await git.silent(true).clone(urlWithAuth, this.localPath, gitOptions)
        }

        if (this.config.specRetrievalGitCommitID !== '') {
            await git.silent(true).fetch(urlWithAuth, this.config.specRetrievalGitCommitID)
            await git.silent(true).checkout(this.config.specRetrievalGitCommitID)
        }

        const gitLog = await git.log(['-1'])

        const elapsedTime = Date.now() - startTime
        logger.debug(
            `SpecRetrieverGit: Retrieved specs from git in ` +
                `${elapsedTime} ms. HEAD is ${JSON.stringify(gitLog.latest)}`,
            {
                OperationName: 'openapi-validate.specRetrieverGit.retrieveSpecsImpl'
            }
        )
    }

    /**
     * Determines the git repo exist locally.
     */
    private existGitRepo(): boolean {
        return fs.pathExistsSync(path.join(this.localPath, '.git'))
    }

    /**
     * Injects a personal access token into the repo URL.
     */
    private async addAuthToUrlForAuthModeToken(url: URL): Promise<void> {
        this.verifyHttpsUrl(url)
        this.injectAuthTokenIntoUrl(url, this.config.specRetrievalGitAuthToken)
    }

    /**
     * Adds a token to the repo URL for authentication
     */
    private injectAuthTokenIntoUrl(url: URL, token: string): void {
        url.username = 'x-access-token'
        url.password = token
    }

    /**
     * Ensures the provided URL is using https
     */
    private verifyHttpsUrl(url: URL): void {
        if (url.protocol === 'https:') {
            return
        }

        throw new Error(
            `SpecRetrieverGit: Expected protocol "https:" for auth mode ${this.config.specRetrievalGitAuthMode} but found "${url.protocol}"`
        )
    }
}
