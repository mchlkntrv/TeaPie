## Possible names
-----------------
-	**Capy** – taken by some python lib
-	**Tapi (Test API)** – taken by python lib for API testing
-	**TeaPie (TEst API Extension)** – the most similar is teapy (python lib)

## Acceptance criteria 
-----------------
- **CLI-based** application
- **Convention-based** by default, possibility for **parametrization**
- **Readable changes** in tests within **pull-requests**
- **Support for collections**, which will be independently runnable
- Possibility to have **pre-request** and **post-response** scripts for each _http_ request, respecitevely for each collection
- **Support** for more, switchable **environments**, with **global variables** included 
- **Comprehensive logging** 
- **Comprehensive reporting system** 
- **Comprehensive documentation/manual**
- Easy addition of new tests/collections (e.g. by parametrized command) 
- **Based** in familiar **backend technology (C#)** 
- Native support for **re-trying**
- Native support for **service-bus** testing
- **Smart authorization** - e.g. _OAuth_
- **Intuitive file structure**
- **Optimized performance** – after the main logic is implemented
- **Test runner** will have current context with run-time variables
- **Hierarchical variables** with the possibility of over-write values
- Possibility to **ignore test/collection**, e.g. by custom attribute or in configuration files
- Possibility to **specify order of the tests/collections** (other than default-alphabetical)
- **Native support for seeding** before test/collection
- Possibility to run **single http request manually**
- Possibility to alter **order of the tests according to response** result/**branching**
- **Re-usable scripts**

## To consider
-----------------
- Check possibility of adding `.md` documentation to tests (e.g. by using comments)
    - Commenting is bit trickier, since **multi-line comments** are not natively supported and you have to work-around to achieve them. When you use 3 hashtags, it automatically thinks, that you want to make request.
    - Maybe usage of double `##` can be interesting for our parser, although this is in conflict with `.md` format of **headers**, since these are also marked by hashtags 
- Consider **usage of solution**, since it is the same thing as collection, only difference is in abstraction
- Consider inputs of QA from different company (Descartes)
- Random data seeding (AutoBogus)

## Possible problems
-----------------
- Saving response to file demands usage of third-party CLI command (newman, cURL, HTTPie) 
- Although, you can still use request variables (side by side with environment, file and prompt variables)
- It is impossible to comment the contents of the .json file in request body, which make us unable to use own variables/comments.

## Definitions
-----------------
**Script** - indepedently runnable piece of code.

**Request** - HTTP request, which is stored in _.http file_.

**Test** - logically encapsulates _HTTP request, pre-request script(s), post-response script(s), testing scenarios, documentation and configuration._ None of these parts is mandatory except HTTP request. Test can have all its dependencies in one folder (default setting), or it can be distributed. By convention, one folder contains one test, but it is possible for folder to contain more tests altogether.

**Collection** - set of tests, which can be somehow (contextually) connected. It can contain other collections - hierarchical approach.

## Naming conventions
-----------------
**kebab-case** - file's name extensions, which specify the type/purpose of the file (-prereq, -req, -postres, -script...)

## File structure 
-----------------

### Single test
Each test can be in own folder, or it can be altogether with another tests.

**Structure:**

- `TestName.json [OPTIONAL]`

- `TestName.md [OPTIONAL]` 

- `TestName-prereq.csx [OPTIONAL]`

- `TestName.http` 

- `TestName-postres.csx [OPTIONAL]` 

 
**Files specification:**

`TestName.json [OPTIONAL]`
- describes configuration of test (re-trying options, await)
- overrides its collection's configuration
- contains variables specific for the test
- adds new variables besides environment and collection variables
- overrides values of same-name variables (located on global, environment and collection levels)
- template: [Test Configuration Template](#test-configuration-file)

`TestName.md [OPTIONAL]`
- optional human-readable description of the test 

`TestName-prereq.csx [OPTIONAL]` 
- pre-request script that can alter behavior before sending a request 

`TestName.http`
- request definition in .http format
- from the beginning, it will contain only one request, later multiple requests will be supported

`TestName-postres.csx [OPTIONAL]`
- script that defines post-response behavior, mainly define tests 

-----------------
### Collection
Contains at least one test and can contain other collections

**Structure:**
- `Seed [OPTIONAL]`

- `Tests`

- `collection-configuration.json`

- `collection-variables.json [OPTIONAL]`

- `CollectionName.md [OPTIONAL]`

- `CollectionName-prereq.csx [OPTIONAL]`

- `CollectionName-postres.csx [OPTIONAL]` 

 
**Files specification:**
`Seed [OPTIONAL]`
- folder, that contains requests/tests for data initialization

`Tests`
- folder, that contains tests, respectively other inner-collections 

`collection-configuration.json`
- defines configuration of the collection – tests to ignore, their order (if different from alphabetical order), re-trying options, etc.
- template: [Collection Configuration Template](#collection-configuration-file)

`collection-variables.json [OPTIONAL]`
- contains variables specific for the collection
- adds new variables besides environment variables
- overrides values of same-name variables (located on global and environment level)
- template: [Collection Variables Template](#general-variables-file)

`collection-description.md [OPTIONAL]`
- human-readable description of collection

`CollectionName-prereq.csx [OPTIONAL]` 
- pre-request script that can alter behavior before collection run

`CollectionName-postres.csx [OPTIONAL]`
- script which is called after finish of the collection

-----------------
### Solution [OPTIONAL]
The highest level folder, which encapsulates all desired collections altogether with environment files, shared scripts and general configuration. Collections and tests are runnable without this folder. Basically it is only logical frame over a single super-collection.

**Structure:**
- `Seed [OPTIONAL]`
- `Collections`
- `Environments`
- `Shared`
- `global-variables.json`
- `solution-configuration.json`

**Files specification:**
`Seed [OPTIONAL]`
- folder, that contains tests for data initialization for all collections/tests

`Collections`
- folder, that contains collections and tests

`Environments`
- folder, that contains supported environments
- template: [Environment Template](#environment-file)

`Shared`
- folder, that contains functionality that can be re-used all over solution (scripts)

`global-variables.json`
- contains variables which will be available across all environments
- template: [Global Variables Template](#general-variables-file)

`solution-configuration.json`
- defines configuration of the solution – collections/tests to ignore, their order (if different from alphabetical order), re-trying options, etc. 

## Templates
-----------------
### Test Configuration File
[Go Back](#single-test)
```json
{
    "retrying": {
        "retrying": boolean,
        "retryingCount": number
    },
    "await": {
        "awaitResponse": boolean,
        "delayTestFor": number, // in milliseconds
        "maxWaitingPeriod": number // in milliseconds
    },
    "variables": [
        {
            "name": string,
            "value": any,
        },
        {
            "name": string,
            "value": any,
        },
        {
            "name": string,
            "value": any,
        }
    ]
}
```

### General Variables File
[Go Back to Test](#single-test)

[Go Back to Collection](#collection)

[Go Back to Solution](#solution-optional)

```json
[
    {
        "name": string,
        "value": any,
    },
    {
        "name": string,
        "value": any,
    },
    {
        "name": string,
        "value": any,
    },
    {
        "name": string,
        "value": any,
    }
]
```

### Collection Configuration File
[Go Back](#collection)
```json
{
    "ignore": {
        "ignoreCollections": [], // names of collections to ignore
        "ignoreTests": [] // names of tests to ignore
    },
    "testsOrder": [], // names of tests and collections in desired order, if empty, alphabetical order is applied
    "retrying": {
        "retrying": boolean,
        "retryingCount": number
    },
    "await": {
        "awaitResponse": boolean,
        "delayTestFor": number, // in milliseconds
        "maxWaitingPeriod": number // in milliseconds
    }
}
```


### Environment File
[Go Back](#solution-optional)
```json
{
    "name": "",
    "version": "",
    "variables": [
        {
            "name": "",
            "value": ""
        },
        {
            "name": "",
            "value": ""
        },
        {
            "name": "",
            "value": ""
        }
    ]    
}
```