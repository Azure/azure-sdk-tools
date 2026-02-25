import { BlobClientManager } from './blobClient.js';
import { ChannelConfigManager } from './channel.js';
import { TenantConfigManager } from './tenant.js';

/**
 * Facade for initializing all configuration managers.
 * Provides a single entry point for config initialization and access.
 */
class ConfigFacade {
  private static channelConfigManager: ChannelConfigManager;
  private static tenantConfigManager: TenantConfigManager;
  private static initialized = false;

  /**
   * Initialize all configuration managers.
   * This should be called once at application startup.
   */
  public static async initialize(): Promise<void> {
    if (this.initialized) {
      return;
    }

    // Initialize shared blob client first
    const blobClientManager = BlobClientManager.getInstance();
    await blobClientManager.initialize();

    // Create config managers
    this.channelConfigManager = new ChannelConfigManager();
    this.tenantConfigManager = new TenantConfigManager();

    // Initialize config managers in parallel
    await Promise.all([
      this.channelConfigManager.initialize(),
      this.tenantConfigManager.initialize(),
    ]);

    this.initialized = true;
  }

  /**
   * Get the ChannelConfigManager instance.
   * @throws Error if ConfigFacade has not been initialized.
   */
  public static getChannelConfigManager(): ChannelConfigManager {
    if (!this.channelConfigManager) {
      throw new Error('ConfigFacade not initialized. Call initialize() first.');
    }
    return this.channelConfigManager;
  }

  /**
   * Get the TenantConfigManager instance.
   * @throws Error if ConfigFacade has not been initialized.
   */
  public static getTenantConfigManager(): TenantConfigManager {
    if (!this.tenantConfigManager) {
      throw new Error('ConfigFacade not initialized. Call initialize() first.');
    }
    return this.tenantConfigManager;
  }

  /**
   * Stop watching all configuration files.
   * Call this during graceful shutdown.
   */
  public static stopWatching(): void {
    this.channelConfigManager?.stopWatching();
    this.tenantConfigManager?.stopWatching();
  }
}

export { ConfigFacade };
