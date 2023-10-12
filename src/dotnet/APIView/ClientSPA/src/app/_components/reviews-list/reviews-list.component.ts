import { Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges, ViewChild } from '@angular/core';
import { FormBuilder, FormControl, FormGroup, Validators } from '@angular/forms';

import { FirstReleaseApproval, Review, SelectItemModel } from 'src/app/_models/review';
import { ReviewsService } from 'src/app/_services/reviews/reviews.service';
import { Pagination } from 'src/app/_models/pagination';
import { Table, TableFilterEvent, TableLazyLoadEvent, TableRowSelectEvent } from 'primeng/table';
import { MenuItem, SortEvent } from 'primeng/api';
import { FileSelectEvent, FileUpload } from 'primeng/fileupload';
import { RevisionsService } from 'src/app/_services/revisions/revisions.service';
import { environment } from 'src/environments/environment';

@Component({
  selector: 'app-reviews-list',
  templateUrl: './reviews-list.component.html',
  styleUrls: ['./reviews-list.component.scss']
})
export class ReviewsListComponent implements OnInit, OnChanges {
  @Output() reviewEmitter : EventEmitter<Review> = new EventEmitter<Review>();
  @Output() clearTableFiltersEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @ViewChild("reviewCreationFileUpload") reviewCreationFileUpload!: FileUpload;

  assetsPath : string = environment.assetsPath;
  reviews : Review[] = [];
  totalNumberOfReviews = 0;
  pagination: Pagination | undefined;
  insertIndex : number = 0;
  resetReviews = false;
  rowHeight: number = 43;
  noOfRows: number = Math.floor((window.innerHeight * 0.75) / this.rowHeight); // Dynamically Computing the number of rows to show at once
  pageSize = 20; // No of items to load from server at a time
  sortField : string = "lastUpdatedOn";
  sortOrder : number = 1;
  filters: any = null;

  sidebarVisible : boolean = false;

  // Filter Options
  languages: SelectItemModel[] = [];
  selectedLanguages: SelectItemModel[] = [];
  @Input() firstReleaseApproval : FirstReleaseApproval = FirstReleaseApproval.All;

  // Context Menu
  contextMenuItems! : MenuItem[];
  selectedReview!: Review;
  selectedReviews!: Review[];
  showSelectionAction : boolean = false;

  // Messages
  reviewListDetail: string = "";

  // Create Review Selections
  crLanguages: any[] = [];
  createReviewForm! : FormGroup;
  creatingReview : boolean = false;
  crButtonText : string = "Create Review";

  badgeClass : Map<string, string> = new Map<string, string>();

  // Review Upload Instructions
  createReviewInstruction : string[] | undefined;
  acceptedFilesForReviewUpload : string | undefined;

  constructor(private reviewsService: ReviewsService,  private apiRevisionsService: RevisionsService, private fb: FormBuilder) { }

  ngOnInit(): void {
    this.loadReviews(0, this.pageSize * 2, true); // Initial load of 2 pages
    this.createFilters();
    this.createContextMenuItems();
    this.createReviewFormGroup();
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['firstReleaseApproval'].previousValue != changes['firstReleaseApproval'].currentValue){
      this.loadReviews(0, this.pageSize * 2, true);
      const firstReleaseApprovalValue = FirstReleaseApproval[changes['firstReleaseApproval'].currentValue];
      this.reviewListDetail = (firstReleaseApprovalValue != "All") ? firstReleaseApprovalValue: "";
    }
  }

  /**
   * Load reviews from API
   *  * @param append wheather to add to or replace existing list
   */
  loadReviews(noOfItemsRead : number, pageSize: number, resetReviews = false, filters: any = null, sortField: string ="lastUpdatedOn",  sortOrder: number = 1) {
    // Reset Filter if necessary
    if (this.filters && this.filters.languages.value == null) {
      this.selectedLanguages = [];
    }

    let packageName : string = "";
    let languages : string [] = [];
    if (filters) {
      packageName = filters.packageName.value ?? packageName;
      languages = (filters.languages.value != null)? filters.languages.value.map((item: any) => item.data) : languages;
    }

    this.reviewsService.getReviews(
      noOfItemsRead, pageSize, packageName, languages, 
      FirstReleaseApproval[this.firstReleaseApproval], sortField, sortOrder).subscribe({
      next: (response : any) => {
        if (response.result && response.pagination) {
          if (resetReviews) {
            const arraySize = Math.ceil(response.pagination!.totalCount + Math.min(20, (0.05 * response.pagination!.totalCount))) // Add 5% extra rows to avoid flikering
            this.reviews = Array.from({ length: arraySize  });
            this.insertIndex = 0;
          }

          if (response.result.length > 0) {
            this.reviews.splice(this.insertIndex, this.insertIndex + response.result.length, ...response.result);
            this.insertIndex = this.insertIndex + response.result.length;
            this.pagination = response.pagination;
            this.totalNumberOfReviews = this.pagination?.totalCount!;
          }
        }
      }
    });
  }

  createContextMenuItems() {
    this.contextMenuItems = [
      { label: 'View', icon: 'pi pi-folder-open', command: () => this.viewReview(this.selectedReview) },
    ];
  }

  createFilters() {
    this.languages = this.crLanguages = [
        { label: "C", data: "C" },
        { label: "C#", data: "C#" },
        { label: "C++", data: "C++" },
        { label: "Go", data: "Go" },
        { label: "Java", data: "Java" },
        { label: "JavaScript", data: "JavaScript" },
        { label: "Json", data: "Json" },
        { label: "Kotlin", data: "Kotlin" },
        { label: "Python", data: "Python" },
        { label: "Swagger", data: "Swagger" },
        { label: "Swift", data: "Swift" },
        { label: "TypeSpec", data: "TypeSpec" },
        { label: "Xml", data: "Xml" }
    ];
  }

  createReviewFormGroup() {
    this.createReviewForm = this.fb.group({
      selectedCRLanguage: [null, Validators.required],
      selectedFile: [null, Validators.required],
      filePath: [null, Validators.required],
      label: [null, Validators.required]
    });
    this.createReviewForm.get('selectedFile')?.disable();
    this.createReviewForm.get('filePath')?.disable();
  }

  viewReview(review: Review) {
    this.reviewsService.openReviewPage(review.id);
  }                   

  /**
   * Return true if table has filters applied.
   */
  tableHasFilters() : boolean {
    return (this.sortField != "lastUpdatedOn" || this.sortOrder != 1 || 
    (this.filters && (this.filters.packageName.value != null || this.filters.languages.value != null)) ||
    this.firstReleaseApproval != FirstReleaseApproval.All);
  }

  /**
   * Clear all filters in Table
   */
  clear(table: Table) {
    table.clear();
    this.loadReviews(0, this.pageSize, true, this.filters, this.sortField, this.sortOrder);
    this.clearTableFiltersEmitter.emit(true);
  }

  /**
   * Callback to invoke on scroll /lazy load.
   * @param event the lazyload event
   */
  onLazyLoad(event: TableLazyLoadEvent) {
    const last = Math.min(event.last!, this.totalNumberOfReviews);
    this.sortField = event.sortField as string ?? "lastUpdatedOn";
    this.sortOrder = event.sortOrder as number ?? 1;
    this.filters = event.filters;
    if (last > (this.insertIndex - this.pageSize)) {
      if (this.pagination && this.pagination?.noOfItemsRead! < this.pagination?.totalCount!) {
        this.loadReviews(this.pagination!.noOfItemsRead, this.pageSize, this.resetReviews, this.filters, this.sortField, this.sortOrder);
      }
    }
    event.forceUpdate!();
  }

  /**
   * Callback to invoke on table filter.
   * @param event the Filter event
   */
  onFilter(event: TableFilterEvent) {
    this.filters = event.filters;
    this.loadReviews(0, this.pageSize, true, this.filters, this.sortField, this.sortOrder);
  }

  /**
   * Callback to invoke on row selection.
   * @param event the Filter event
   */
  onRowSelect(event: TableRowSelectEvent) {
    this.reviewEmitter.emit(event.data);
  }

  /**
   * Callback to invoke on column sort.
   * @param event the Filter event
   */
  onSort(event: SortEvent) {
    this.sortField = event.field as string ?? "packageName";
    this.sortOrder = event.order as number ?? 1;
    this.loadReviews(0, this.pageSize, true, this.filters, this.sortField, this.sortOrder);
  }

  /**
   * Callback to invoke on file selction for review creation
   * @param event the Filter event
   */
  onFileSelect(event: FileSelectEvent) {
    const uploadFile = event.currentFiles[0];
    this.createReviewForm.get('selectedFile')?.setValue(uploadFile);
  }

  // Show or hide the sidebar for creating a review
  onHideSideBar() {
    this.createReviewForm.reset();
    this.createReviewInstruction = [];
  }

  // Fire API request to create the review
  createReview() {
    if (this.createReviewForm.valid) {

      const formData: FormData = new FormData();
      formData.append("label", this.createReviewForm.get('label')?.value!);
      formData.append("language", this.createReviewForm.get('selectedCRLanguage')?.value?.data!);

      if (this.createReviewForm.get('filePath')?.value) {
        formData.append("filePath", this.createReviewForm.get('filePath')?.value!);
      }

      if (this.createReviewForm.get('selectedFile')?.value) {
        const file = this.createReviewForm.get('selectedFile')?.value as File;
        formData.append("file", file, file.name);
      }

      this.creatingReview = true;
      this.crButtonText = "Creating Review ";

      this.reviewsService.createReview(formData).subscribe({
        next: (response: any) => {
          if (response) {
            this.sidebarVisible = false;
            this.creatingReview = false;
            this.crButtonText = "Create Review";
            this.apiRevisionsService.openAPIRevisionPage(response.reviewId, response.id);
          }
        }
      });
    }
  }

  onCRLanguageSelectChange() {
    switch(this.createReviewForm.get('selectedCRLanguage')?.value?.data){
      case "C":
        this.createReviewInstruction = [
          `Install clang 10 or later.`, 
          `Run <code>clang [inputs like az_*.h] -Xclang -ast-dump=json -I ..\\..\\..\\core\\core\\inc -I "c:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\Preview\\VC\\Tools\\MSVC\\14.26.28801\\include\\" > az_core.ast</code>`,
          `Archive the file <code>Compress-Archive az_core.ast -DestinationPath az_core.zip</code>`,
          `Upload the resulting archive.`
        ];
        this.acceptedFilesForReviewUpload = ".zip";
        this.createReviewForm.get('selectedFile')?.enable();
        this.createReviewForm.get('filePath')?.disable();
        break;
      case "C#":
        this.createReviewInstruction = [
          `Run <code>dotnet pack</code>`, 
          `Upload the resulting .nupkg file.`
        ];
        this.acceptedFilesForReviewUpload = ".nupkg";
        this.createReviewForm.get('selectedFile')?.enable();
        this.createReviewForm.get('filePath')?.disable();
        break;
      case "C++":
        this.createReviewInstruction = [
          `Generate a token file using the <a href="https://github.com/Azure/azure-sdk-tools/tree/main/tools/apiview/parsers/cpp-api-parser#readme">C++ parser</a>`,
          `Upload the token file generated.`
        ];
        this.acceptedFilesForReviewUpload = ".json";
        this.createReviewForm.get('selectedFile')?.enable();
        this.createReviewForm.get('filePath')?.disable();
        break;
      case "Java":
        this.createReviewInstruction = [
          `Run a <code>mvn package</code> build on your project, which will generate a number of build artifacts in the <code>/target</code> directory. In there, find the file ending <code>sources.jar</code>, and select it.`,
        ];
        this.acceptedFilesForReviewUpload = ".sources.jar";
        this.createReviewForm.get('selectedFile')?.enable();
        this.createReviewForm.get('filePath')?.disable();
        break;
      case "Python":
        this.createReviewInstruction = [
          `Generate wheel for the package. <code>python setup.py bdist_wheel -d [dest_folder]</code>`,
          `Upload generated whl file`
        ];
        this.acceptedFilesForReviewUpload = ".whl";
        this.createReviewForm.get('selectedFile')?.enable();
        this.createReviewForm.get('filePath')?.disable();
        break;
      case "JavaScript":
        this.createReviewInstruction = [
          `Use <code>api-extractor</code> to generate a <a href="https://api-extractor.com/pages/setup/generating_docs/">docModel file</a>`,
          `Upload generated api.json file`
        ];
        this.acceptedFilesForReviewUpload = ".json";
        this.createReviewForm.get('selectedFile')?.enable();
        this.createReviewForm.get('filePath')?.disable();
        break;
      case "Go":
        this.createReviewInstruction = [
          `Archive source module directory in which go.mod is present. <code>Compress-Archive ./sdk/azcore -DestinationPath azcore.zip</code>`,
          `Rename the file <code>Rename-Item azcore.zip -NewName  azcore.gosource</code>`,
          `Upload the resulting archive.`
        ];
        this.acceptedFilesForReviewUpload = ".gosource";
        this.createReviewForm.get('selectedFile')?.enable();
        this.createReviewForm.get('filePath')?.disable();
        break;
      case "Swift":
        this.createReviewInstruction = [
          `Generate JSON file for the source by running Swift APIView parser in XCode. More information is available here on <a href="https://github.com/Azure/azure-sdk-tools/blob/main/src/swift/README.md">Swift API parser</a>`,
          `Upload generated JSON`
        ];
        this.acceptedFilesForReviewUpload = ".json";
        this.createReviewForm.get('selectedFile')?.enable();
        this.createReviewForm.get('filePath')?.disable();
        break;
      case "Swagger":
        this.createReviewInstruction = [
          `Rename swagger json to replace file extension to .swagger  <code>Rename-Item PetSwagger.json -NewName PetSwagger.swagger</code>`,
          `Upload renamed swagger file`
        ];
        this.acceptedFilesForReviewUpload = ".swagger";
        this.createReviewForm.get('selectedFile')?.enable();
        this.createReviewForm.get('filePath')?.disable();
        break;
      case "TypeSpec":
        this.createReviewInstruction = [
          `Rename swagger json to replace file extension to .swagger  <code>Rename-Item PetSwagger.json -NewName PetSwagger.swagger</code>`,
          `Upload renamed swagger file`
        ];
        this.acceptedFilesForReviewUpload = ".json";
        this.createReviewForm.get('selectedFile')?.disable();
        this.createReviewForm.get('filePath')?.enable();
        break;
      case "Json":
        this.createReviewInstruction = [
          `Upload JSON API review token file.`
        ];
        this.acceptedFilesForReviewUpload = ".json";
        this.createReviewForm.get('selectedFile')?.enable();
        this.createReviewForm.get('filePath')?.disable();
        break;
      default:
        this.createReviewInstruction = []
    }

    if (this.reviewCreationFileUpload) {    
      this.reviewCreationFileUpload.clear();
    }

    this.createReviewForm.get('label')?.reset();
    this.createReviewForm.get('selectedFile')?.reset();
    this.createReviewForm.get('filePath')?.reset()
  }
}
