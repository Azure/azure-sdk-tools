export const fixtures = {
  specTest: {
    name: 'spec-test',
    patch0_AddService: 'spec-test-patch0-add-service',
    patch1_Add_02_01: 'spec-test-patch1-add-02-01', // Depends on patch0
    patch2_Empty: 'spec-test-patch2-empty-update', // Depends on patch0
    patch3_TwoReadme: 'spec-test-patch3-two-readme'
  },
  sdkGo: {
    name: 'sdk-go-test'
  },
  sdkJs: {
    name: 'sdk-js-test',
    patch0_AddServiceGen: 'sdk-js-test-patch0-add-service-gen' // CodeGen result after spec patch0
  },
  sdkPy: {
    name: 'sdk-py-test',
    patch0_Track2: 'sdk-py-test-patch0-track2' // Track2 config
  },
  sdkTf: {
    name: 'sdk-tf-test'
  },
  schmARM: {
    name: 'schm-arm-test'
  }
} as const;
