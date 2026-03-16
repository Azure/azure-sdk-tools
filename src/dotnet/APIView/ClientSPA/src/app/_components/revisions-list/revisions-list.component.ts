import { Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MenuItem, MessageService, SortEvent } from 'primeng/api';
import { FileSelectEvent, FileUpload, FileUploadModule } from 'primeng/fileupload';
import { Table, TableContextMenuSelectEvent, TableFilterEvent, TableLazyLoadEvent, TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { MenuModule } from 'primeng/menu';
import { DrawerModule } from 'primeng/drawer';
import { SelectModule } from 'primeng/select';
import { MultiSelectModule } from 'primeng/multiselect';
import { ContextMenuModule } from 'primeng/contextmenu';
import { TimeagoModule } from 'ngx-timeago';
import { LanguageNamesPipe } from 'src/app/_pipes/language-names.pipe';
import { LastUpdatedOnPipe } from 'src/app/_pipes/last-updated-on.pipe';
import { Pagination } from 'src/app/_models/pagination';
import { Review } from 'src/app/_models/review';
import { APIRevision } from 'src/app/_models/revision';
import { UserProfile } from 'src/app/_models/userProfile';
import { ConfigService } from 'src/app/_services/config/config.service';
import { ReviewsService } from 'src/app/_services/reviews/reviews.service';
import { APIRevisionsService } from 'src/app/_services/revisions/revisions.service';
import { environment } from 'src/environments/environment';
import { getSupportedLanguages } from 'src/app/_helpers/common-helpers';

@Component({
    selector: 'app-revisions-list',
    templateUrl: './revisions-list.component.html',
    styleUrls: ['./revisions-list.component.scss'],
    standalone: true,
    imports: [
        CommonModule,
        ReactiveFormsModule,
        FormsModule,
        RouterModule,
        TableModule,
        ButtonModule,
        MenuModule,
        FileUploadModule,
        DrawerModule,
        SelectModule,
        MultiSelectModule,
        ContextMenuModule,
        LanguageNamesPipe,
        LastUpdatedOnPipe,
        TimeagoModule
    ]
})
export class RevisionsListComponent implements OnInit, OnChanges {
  @Input() review : Review | undefined = undefined;
  @Input() revisionSidePanel : boolean = false;
  @Input() userProfile: UserProfile | undefined;

  @Output() apiRevisionsEmitter : EventEmitter<APIRevision[]> = new EventEmitter<APIRevision[]>();

  @ViewChild("revisionCreationFileUpload") revisionCreationFileUpload!: FileUpload;

  assetsPath : string = environment.assetsPath;
  reviewPageWebAppUrl : string = this.configService.webAppUrl + "Assemblies/Review/";
  profilePageWebAppUrl : string = this.configService.webAppUrl + "Assemblies/Profile/";
  revisions : APIRevision[] = [];
  totalNumberOfRevisions = 0;
  pagination: Pagination | undefined;
  insertIndex : number = 0;
  rowHeight: number = 48;
  noOfRows: number = Math.floor((window.innerHeight * 0.75) / this.rowHeight); // Dynamically Computing the number of rows to show at once
  pageSize = 20; // No of items to load from server at a time
  sortField : string = "lastUpdatedOn";
  sortOrder : number = 1;
  filters: any = null;
  dummyModel: any; // Used to ensure p-fileUpload has a binding

  createRevisionSidebarVisible : boolean = false;
  optionsSidebarVisible : boolean = false;

  // Create Revision Selections
  createRevisionForm! : FormGroup;
  crLanguages: any[] = [];
  creatingRevision : boolean = false;
  crButtonText : string = "Create Review";

  // Review Upload Instructions
  createRevisionInstruction : string[] | undefined;
  acceptedFilesForReviewUpload : string | undefined;

  // Filters
  details: any[] = [];
  selectedDetails: any[] = [];
  private _showDeletedAPIRevisions : boolean = false;
  private _showAPIRevisionsAssignedToMe : boolean = false;

  // Context Menu
  contextMenuItems! : MenuItem[];
  selectedRevision!: APIRevision;
  selectedRevisions!: APIRevision[];
  showSelectionActions : boolean = false;
  showDiffButton : boolean = false;
  showDeleteButton : boolean = false;

  // Messages
  apiRevisionsListDetail: string = "APIRevision(s) from"

  badgeClass : Map<string, string> = new Map<string, string>();

  constructor(private apiRevisionsService: APIRevisionsService,
    private configService: ConfigService, private fb: FormBuilder, private reviewsService: ReviewsService,
    private route: ActivatedRoute, private messageService: MessageService) { }

  ngOnInit(): void {
    this.createRevisionFilters();
    this.createLanguageFilters();
    this.setDetailsIcons();
    if (!this.review) {
      this.loadAPIRevisions(0, this.pageSize * 2, true);
      this.createRevisionFormGroup();
    }
  }

  ngAfterOnit() {
    if (this.review) {
      this.loadAPIRevisions(0, this.pageSize * 2, true);
    }
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['review'] && changes['review'].previousValue != changes['review'].currentValue){
      if (this.showAPIRevisionsAssignedToMe)
      {
        this.toggleShowAPIRevisionsAssignedToMe();
      }
      else {
        this.loadAPIRevisions(0, this.pageSize * 2, true);
      }
      this.createRevisionFormGroup();
      this.showSelectionActions = false;
      this.showDiffButton = false;
    }

    if (changes['revisionSidePanel'] && changes['revisionSidePanel'].currentValue == false) {
      this.createRevisionSidebarVisible = false;
      this.optionsSidebarVisible = false;
    }
  }

  /**
   * Load revision from API
   *  * @param append wheather to add to or replace existing list
   */
  loadAPIRevisions(noOfItemsRead : number, pageSize: number, resetReviews = false, filters: any = null, sortField: string = "lastUpdatedOn",  sortOrder: number = 1) {
    let label : string = "";
    let author : string = "";
    let reviewId: string = this.review?.id ?? "";
    let details : string [] = [];
    if (filters)
    {
      label = filters.label.value ?? label;
      author = filters.author.value ?? author;
      details = (filters.details.value != null) ? filters.details.value.map((item: any) => item.data): details;
    }

    this.apiRevisionsService.getAPIRevisions(noOfItemsRead, pageSize, reviewId, label, author, details, sortField, sortOrder,
      this.showDeletedAPIRevisions, this.showAPIRevisionsAssignedToMe).subscribe({
      next: (response: any) => {
        if (response.result && response.pagination) {
          if (resetReviews)
          {
            const arraySize = Math.ceil(response.pagination!.totalCount + Math.min(20, (0.05 * response.pagination!.totalCount))) // Add 5% extra rows to avoid flikering
            this.revisions = Array.from({ length: arraySize });
            this.insertIndex = 0;
            this.showSelectionActions = false;
            this.showDiffButton = false;
          }

          if (response.result.length > 0)
          {
            this.revisions.splice(this.insertIndex, this.insertIndex + response.result.length, ...response.result);
            this.insertIndex = this.insertIndex + response.result.length;
            this.pagination = response.pagination;
            this.totalNumberOfRevisions = this.pagination?.totalCount!;
          }
          this.apiRevisionsEmitter.emit(this.revisions);
          this.setCreateRevisionLanguageBasedOnReview();
        }
      }
    });
  }

  createRevisionFormGroup() {
    this.createRevisionForm = this.fb.group({
      selectedCRLanguage: [null, Validators.required],
      selectedFile: [null, Validators.required],
      filePath: [null, Validators.required],
      label: [null, Validators.required]
    });
    this.createRevisionForm.get('selectedFile')?.disable();
    this.createRevisionForm.get('filePath')?.disable();
  }

  createContextMenuItems(apiRevision: APIRevision) {
    const disableDeleteOrRestore  = (apiRevision.apiRevisionType == "manual" && apiRevision.createdBy == this.userProfile?.userName) ? false : true;
    if (this.showDeletedAPIRevisions)
    {
      this.contextMenuItems = [
        { label: 'Restore', icon: 'pi pi-folder-open', disabled: disableDeleteOrRestore, command: () => this.viewRevision(this.selectedRevision) }
      ];
    }
    else
    {
      this.contextMenuItems = [
        { label: 'View', icon: 'pi pi-folder-open', command: () => this.viewRevision(this.selectedRevision) },
        { label: 'Delete', icon: 'pi pi-fw pi-times', disabled: disableDeleteOrRestore, command: () => this.deleteRevision(this.selectedRevision) }
      ];
    }
  }

  createLanguageFilters() {
    this.crLanguages = getSupportedLanguages();
  }

  createRevisionFilters() {
    this.details = [
      {
        label: 'Status',
        data: 'All',
        items: [
          { label: "Approved", data: "Approved" },
          { label: "Pending", data: "Pending" },
        ]
      },
      {
        label: 'Type',
        data: 'All',
        items: [
          { label: "Automatic", data: "Automatic" },
          { label: "Manual", data: "Manual" },
          { label: "Pull Request", data: "PullRequest" }
        ]
      }
    ];
  }

  setDetailsIcons(){
    // Set Badge Class for details Icons
    this.badgeClass.set("false", "fa-solid fa-circle-minus text-warning");
    this.badgeClass.set("true", "fas fa-check-circle text-success");
    this.badgeClass.set("Manual", "fa-solid fa-arrow-up-from-bracket");
    this.badgeClass.set("PullRequest", "fa-solid fa-code-pull-request");
    this.badgeClass.set("Automatic", "fa-solid fa-robot");
  }

  viewDiffOfSelectedAPIRevisions() {
    if (this.selectedRevisions.length == 2)
    {
      this.apiRevisionsService.openDiffOfAPIRevisions(this.selectedRevisions[0], this.selectedRevisions[1], this.route);
    }
  }

  viewRevision(apiRevision: APIRevision) {
    if (!this.showDeletedAPIRevisions)
    {
      this.apiRevisionsService.openAPIRevisionPage(apiRevision, this.route);
    }
  }

  deleteRevisions(revisions: APIRevision []) {
    this.apiRevisionsService.deleteAPIRevisions(this.review!.id, revisions.map(r => r.id)).subscribe({
      next: (response: any) => {
        if (response) {
          this.loadAPIRevisions(0, this.pageSize * 2, true);
          this.clearActionButtons();
        }
      }
    });
  }

  restoreRevisions(revisions: APIRevision []) {
    this.apiRevisionsService.restoreAPIRevisions(this.review!.id, revisions.map(r => r.id)).subscribe({
      next: (response: any) => {
        if (response) {
          this.loadAPIRevisions(0, this.pageSize * 2, true);
          this.clearActionButtons();
        }
      }
    });
  }

  deleteRevision(revision: APIRevision) {
    this.apiRevisionsService.deleteAPIRevisions(revision.reviewId, [revision.id]).subscribe({
      next: (response: any) => {
        if (response) {
          this.loadAPIRevisions(0, this.pageSize * 2, true);
          this.clearActionButtons();
        }
      }
    });
  }

  clearActionButtons() {
    this.selectedRevisions = [];
    this.showSelectionActions = false;
    this.showDiffButton = false;
    this.showDeleteButton = false;
  }

  /**
  * Return true if table has filters applied.
  */
  tableHasFilters() : boolean {
    return (
      this.sortField != "lastUpdatedOn" || this.sortOrder != 1 ||
      (this.filters && (this.filters.label.value != null || this.filters.author.value != null || this.filters.details.value != null)) ||
      this.showDeletedAPIRevisions || this.showAPIRevisionsAssignedToMe);
  }

  /**
  * Clear all filters in Table
  */
  clear(table: Table | undefined = undefined) {
    if (table) {
      table.clear();
    }
    this.showAPIRevisionsAssignedToMe = false;
    this.showDeletedAPIRevisions = false;
    this.loadAPIRevisions(0, this.pageSize * 2, true);
  }

  /**
  * Clear selected items on the page
  */
  clearSelection() {
    this.selectedRevisions = []
    this.showSelectionActions = false;
    this.showDeleteButton = false;
  }

  /**
  * Toggle Show deleted APIRevisions
  */
  toggleShowDeletedAPIRevisions() {
    this.showDeletedAPIRevisions = !this.showDeletedAPIRevisions;
    this.showAPIRevisionsAssignedToMe = false;
    this.loadAPIRevisions(0, this.pageSize * 2, true);
  }

  /**
  * Toggle Show APIRevisions Assigned to Me
  */
  toggleShowAPIRevisionsAssignedToMe() {
    this.showAPIRevisionsAssignedToMe = !this.showAPIRevisionsAssignedToMe;
    this.showDeletedAPIRevisions = false;
    if (this.showAPIRevisionsAssignedToMe) {
      this.review = undefined;
    }
    this.loadAPIRevisions(0, this.pageSize * 2, true);
  }

  updateAPIRevisoinsListDetails() {
    let msg = "APIRevision(s)";
    if (this.showDeletedAPIRevisions)
    {
      msg = "Deleted " + msg;
    }
    if (this.showAPIRevisionsAssignedToMe)
    {
      msg = msg + " Assigned to Me";
    }
    msg = msg + " from";
    this.apiRevisionsListDetail = msg;
  }

  setCreateRevisionLanguageBasedOnReview() {
    if (this.review) {
      this.createRevisionForm.patchValue({
        selectedCRLanguage: this.crLanguages.filter((item) => item.label == this.review?.language)[0],
      });
      this.onCRLanguageSelectChange();
    }
  }

  // Getters and Setters
  get showDeletedAPIRevisions(): boolean {
    return this._showDeletedAPIRevisions;
  }
  set showDeletedAPIRevisions(value: boolean) {
    this._showDeletedAPIRevisions = value;
    this.updateAPIRevisoinsListDetails();
  }

  get showAPIRevisionsAssignedToMe(): boolean {
    return this._showAPIRevisionsAssignedToMe;
  }
  set showAPIRevisionsAssignedToMe(value: boolean) {
    this._showAPIRevisionsAssignedToMe = value;
    this.updateAPIRevisoinsListDetails();
  }

  /**
   * Callback to invoke on scroll /lazy load.
   * @param event the lazyload event
   */
  onLazyLoad(event: TableLazyLoadEvent) {
      const last = Math.min(event.last!, this.totalNumberOfRevisions);
      this.sortField = event.sortField as string ?? "lastUpdatedOn";
      this.sortOrder = event.sortOrder as number ?? 1;
      this.filters = event.filters;
      if (last! > (this.insertIndex - this.pageSize))
      {
        if (this.pagination && this.pagination?.noOfItemsRead! < this.pagination?.totalCount!)
        {
          this.loadAPIRevisions(this.pagination!.noOfItemsRead, this.pageSize, false, event.filters, this.sortField, this.sortOrder);
        }
      }
      event.forceUpdate!();
  }

  /**
   * Callback to invoke on table filter.
   * @param event the Filter event
   */
  onFilter(event: TableFilterEvent) {
    this.loadAPIRevisions(0, this.pageSize, true, event.filters);
  }

  /**
   * Callback to invoke on table selection.
   * @param event the Filter event
   */
  onSelectionChange(value : APIRevision[] = []) {
    this.selectedRevisions = value;
    this.showSelectionActions = (value.length > 0) ? true : false;
    this.showDiffButton = (value.length == 2 && value[0].language == value[1].language) ? true : false;
    let canDelete = (value.length > 0)? true : false;
    for (const revision of value) {
      if (revision.createdBy != this.userProfile?.userName || revision.apiRevisionType != "manual")
      {
        canDelete = false;
        break;
      }
    }
    this.showDeleteButton = canDelete;
  }

  /**
   * Callback to invoke on column sort.
   * @param event the Filter event
   */
  onSort(event: SortEvent) {
    this.loadAPIRevisions(0, this.pageSize, true, null, event.field, event.order);
  }

  // Show or hide the sidebar for creating a review
  onHideCreateRevisionSidebar() {
    this.createRevisionForm.reset();
    this.createRevisionInstruction = [];
    this.setCreateRevisionLanguageBasedOnReview();
  }

  /**
   * Callback to invoke on file selction for review creation
   * @param event the Filter event
   */
  onFileSelect(event: FileSelectEvent) {
    const uploadFile = event.currentFiles[0];
    this.createRevisionForm.get('selectedFile')?.setValue(uploadFile);
  }

  onContextMenuSelect(event : TableContextMenuSelectEvent) {
    this.createContextMenuItems(event.data);
  }

  // Fire API request to create the review
  createRevision() {
    if (this.createRevisionForm.valid) {

      const formData: FormData = new FormData();
      formData.append("label", this.createRevisionForm.get('label')?.value!);
      formData.append("language", this.createRevisionForm.get('selectedCRLanguage')?.value?.data!);

      if (this.createRevisionForm.get('filePath')?.value) {
        formData.append("filePath", this.createRevisionForm.get('filePath')?.value!);
      }

      if (this.createRevisionForm.get('selectedFile')?.value) {
        const file = this.createRevisionForm.get('selectedFile')?.value as File;
        formData.append("file", file, file.name);
      }

      this.creatingRevision = true;
      this.crButtonText = "Creating Review ";

      this.reviewsService.createReview(formData).subscribe({
        next: (response: any) => {
          if (response) {
            this.createRevisionSidebarVisible = false;
            this.creatingRevision = false;
            this.crButtonText = "Create Review";
            this.apiRevisionsService.openAPIRevisionPage(response, this.route);
          }
        },
        error: (error: any) => {
          this.creatingRevision = false;
          this.messageService.add({ severity: 'error', icon: 'bi bi-exclamation-triangle', summary: 'Revision Failure', detail: 'Failed to create new API Revision', key: 'bc', life: 3000 });
        }
      });
    }
  }

  onCRLanguageSelectChange() {
    switch(this.createRevisionForm.get('selectedCRLanguage')?.value?.data){
      case "C":
        this.createRevisionInstruction = [
          `Install clang 10 or later.`,
          `Run <code>clang [inputs like az_*.h] -Xclang -ast-dump=json -I ..\\..\\..\\core\\core\\inc -I "c:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\Preview\\VC\\Tools\\MSVC\\14.26.28801\\include\\" > az_core.ast</code>`,
          `Archive the file <code>Compress-Archive az_core.ast -DestinationPath az_core.zip</code>`,
          `Upload the resulting archive.`
        ];
        this.acceptedFilesForReviewUpload = ".zip";
        this.createRevisionForm.get('selectedFile')?.enable();
        this.createRevisionForm.get('filePath')?.disable();
        break;
      case "C#":
        this.createRevisionInstruction = [
          `Run <code>dotnet pack</code>`,
          `Upload the resulting .nupkg or .dll file.`
        ];
        this.acceptedFilesForReviewUpload = ".nupkg, .dll";
        this.createRevisionForm.get('selectedFile')?.enable();
        this.createRevisionForm.get('filePath')?.disable();
        break;
      case "C++":
        this.createRevisionInstruction = [
          `Generate a token file using the <a href="https://github.com/Azure/azure-sdk-tools/tree/main/tools/apiview/parsers/cpp-api-parser#readme">C++ parser</a>`,
          `Upload the token file generated.`
        ];
        this.acceptedFilesForReviewUpload = ".json";
        this.createRevisionForm.get('selectedFile')?.enable();
        this.createRevisionForm.get('filePath')?.disable();
        break;
      case "Java":
        this.createRevisionInstruction = [
          `Run a <code>mvn package</code> build on your project, which will generate a number of build artifacts in the <code>/target</code> directory. In there, find the file ending <code>sources.jar</code>, and select it.`,
        ];
        this.acceptedFilesForReviewUpload = ".jar";
        this.createRevisionForm.get('selectedFile')?.enable();
        this.createRevisionForm.get('filePath')?.disable();
        break;
      case "Python":
        this.createRevisionInstruction = [
          `Generate wheel for the package. <code>pip install build; python -m build --wheel --outdir [dest_folder]</code>`,
          `Upload generated whl file`
        ];
        this.acceptedFilesForReviewUpload = ".whl";
        this.createRevisionForm.get('selectedFile')?.enable();
        this.createRevisionForm.get('filePath')?.disable();
        break;
      case "JavaScript":
        this.createRevisionInstruction = [
          `Use <code>api-extractor</code> to generate a <a href="https://api-extractor.com/pages/setup/generating_docs/">docModel file</a>`,
          `Upload generated api.json file`
        ];
        this.acceptedFilesForReviewUpload = ".json";
        this.createRevisionForm.get('selectedFile')?.enable();
        this.createRevisionForm.get('filePath')?.disable();
        break;
      case "Go":
        this.createRevisionInstruction = [
          `Archive source module directory in which go.mod is present. <code>Compress-Archive ./sdk/azcore -DestinationPath azcore.zip</code>`,
          `Rename the file <code>Rename-Item azcore.zip -NewName  azcore.gosource</code>`,
          `Upload the resulting archive.`
        ];
        this.acceptedFilesForReviewUpload = ".gosource";
        this.createRevisionForm.get('selectedFile')?.enable();
        this.createRevisionForm.get('filePath')?.disable();
        break;
      case "Rust":
        this.createRevisionInstruction = [
          `In the root of your azure-sdk-for-rust clone, run: <code>cargo run --manifest-path eng/tools/generate_api_report/Cargo.toml -- --package {package-name}</code>`,
          `Upload <code>sdk/{service-name}/{package-name}/review/{package-name}.rust.json</code> using the file picker in this drawer.`
        ];
        this.acceptedFilesForReviewUpload = ".rust.json";
        this.createRevisionForm.get('selectedFile')?.enable();
        this.createRevisionForm.get('filePath')?.disable();
        break;
      case "Swift":
        this.createRevisionInstruction = [
          `Generate JSON file for the source by running Swift APIView parser in XCode. More information is available here on <a href="https://github.com/Azure/azure-sdk-tools/blob/main/src/swift/README.md">Swift API parser</a>`,
          `Upload generated JSON`
        ];
        this.acceptedFilesForReviewUpload = ".json";
        this.createRevisionForm.get('selectedFile')?.enable();
        this.createRevisionForm.get('filePath')?.disable();
        break;
      case "Swagger":
        this.createRevisionInstruction = [
          `Rename swagger json to replace file extension to .swagger  <code>Rename-Item PetSwagger.json -NewName PetSwagger.swagger</code>`,
          `Upload renamed swagger file`
        ];
        this.acceptedFilesForReviewUpload = ".swagger";
        this.createRevisionForm.get('selectedFile')?.enable();
        this.createRevisionForm.get('filePath')?.disable();
        break;
      case "TypeSpec":
        this.createRevisionInstruction = [
          `Rename swagger json to replace file extension to .swagger  <code>Rename-Item PetSwagger.json -NewName PetSwagger.swagger</code>`,
          `Upload renamed swagger file`
        ];
        this.acceptedFilesForReviewUpload = ".json";
        this.createRevisionForm.get('selectedFile')?.disable();
        this.createRevisionForm.get('filePath')?.enable();
        break;
      case "Json":
        this.createRevisionInstruction = [
          `Upload .json API review token file.`
        ];
        this.acceptedFilesForReviewUpload = ".json";
        this.createRevisionForm.get('selectedFile')?.enable();
        this.createRevisionForm.get('filePath')?.disable();
        break;
      default:
        this.createRevisionInstruction = [];
        this.acceptedFilesForReviewUpload = undefined;
    }

    if (this.revisionCreationFileUpload) {
      this.revisionCreationFileUpload.clear();
    }

    this.createRevisionForm.get('label')?.reset();
    this.createRevisionForm.get('selectedFile')?.reset();
    this.createRevisionForm.get('filePath')?.reset()
  }
}
