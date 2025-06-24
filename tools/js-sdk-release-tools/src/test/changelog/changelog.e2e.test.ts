import path from "path";
import { test } from "vitest";
import { SDKType } from "../../common/types.js";
import {
    ChangelogGenerator,
    ChangelogItemCategory,
    ChangelogItems,
} from "../../changelog/v2/ChangelogGenerator.js";
import {
    ApiViewOptions,
    DifferenceDetector,
} from "../../changelog/v2/DifferenceDetector.js";

const getItemsByCategory = (
    changelogItems: ChangelogItems,
    category: ChangelogItemCategory,
) => {
    const isBreakingChange = category >= 10000;
    const map = isBreakingChange
        ? changelogItems.breakingChanges
        : changelogItems.features;
    console.log("🚀 ~ map:", map);
    if (!map) return [];
    console.log(
        "🚀 ~getItemsByCategory changelogItems:",
        changelogItems,
        category,
    );
    return map.get(category) ?? [];
};

const generateChangelogItems = async (
    baselineApiViewOptions: ApiViewOptions,
    currentApiViewOptions: ApiViewOptions,
) => {
    const detector = new DifferenceDetector(
        baselineApiViewOptions,
        currentApiViewOptions,
    );
    const diff = await detector.detect();
    console.log(
        "🚀 ~ generateChangelogItems ~ diff:",
        diff,
        diff.interfaces.get("DataProductsCatalogs_sig_change"),
    );
    const changelogGenerator = new ChangelogGenerator(
        detector.getDetectContext(),
        diff,
    );
    const changelogItems = changelogGenerator.generate().changelogItems;
    console.log(
        "🚀 ~ generateChangelogItems ~ changelogItems:",
        changelogItems,
    );
    return changelogItems;
};

test("xxxxxxx", async () => {
    /// HLC -> MC
    const oldViewPath =
        "C:\\Users\\wanl\\Downloads\\hlc-mc\\arm-chaos-hlc.api.md";
    const newViewPath =
        "C:\\Users\\wanl\\Downloads\\hlc-mc\\arm-chaos-mc.api.md";

    // /// RLC
    // const oldViewPath =
    //     "C:/Users/wanl/workspace/azure-sdk-for-js/sdk/deviceupdate/iot-device-update-rest/review/iot-device-update.api.md";
    // const newViewPath =
    //     "C:/Users/wanl/workspace/azure-sdk-for-js/sdk/deviceupdate/iot-device-update-rest/review/iot-device-update.api copy.md";

    // /// HLC
    // const oldViewPath =
    //     "C:/Users/wanl/workspace/azure-sdk-for-js/sdk/deviceupdate/arm-deviceupdate/review/arm-deviceupdate.api.md";
    // const newViewPath =
    //     "C:/Users/wanl/workspace/azure-sdk-for-js/sdk/deviceupdate/arm-deviceupdate/review/arm-deviceupdate.api copy.md";

    // /// MC
    // const oldViewPath =
    //     "C:/Users/wanl/workspace/azure-sdk-for-js/sdk/mongocluster/arm-mongocluster/review/arm-mongocluster.api.md";
    // const newViewPath =
    //     "C:/Users/wanl/workspace/azure-sdk-for-js/sdk/mongocluster/arm-mongocluster/review/arm-mongocluster.api copy.md";

    const changelogItems = await generateChangelogItems(
        { path: oldViewPath, sdkType: SDKType.HighLevelClient },
        { path: newViewPath, sdkType: SDKType.ModularClient },
    );
    console.log("🚀 ~ test ~ changelogItems:", changelogItems);
});
