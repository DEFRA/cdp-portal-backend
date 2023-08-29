# cdp-portal-backend

A service for tracking deployments (what's deployed and where) across environments and for 
discovering, persisting and providing access to all deployable artifacts on the platform (docker and ECR images).

## How does it work?

tl;dr listens to SQS events for ECR image uploads and ECS deployments, populates a mongodb database with that information,
and provides a RESTful API to access that information for the frontend.

**Deployments - the "what is where"**

`cdp-portal-backend` listens for AWS ECS deployment events on an SQS queue.

When an event comes in it will do the following:
- looks up the aws account id against a list of known environment names
- extracts the container image name and version from the event
- discards any containers that dont already exist in cdp-deployables
- writes the event to the deployments collection

**Artifacts and images - what we can deploy on the portal**

`cdp-portal-backend` listens for SQS events from the AWS ECR repository. When a new container is pushed, this service will download the manifest of the image.
If it hasn't seen the image before it will attempt to retrieve all the layers of the image, including the config layer. As it encounters files/data of interest it will read and
store this data.
Currently it extracts:
- Labels (specifically ones added during the build process to link it to the git repo that created it)
- Package.json (this is an example of how file extraction might work. In practice we'd want to search for package-lock.json etc)

## APIs

### Find Services
When an image is scanned its 'service name' is extracted from a docker label (`defra.cdp.service.name` see ArtifactScanner.cs).

`GET /services`

```json
[
    {
        "serviceName": "cdp-portal-deployables-backend",
        "githubUrl": "https://github.com/defra-cdp-sandpit/cdp-deployables",
        "imageName": "cdp-portal-deployables-backend"
    },
    {
        "serviceName": "cdp-deployments",
        "githubUrl": "https://github.com/defra-cdp-sandpit/cdp-deployments",
        "imageName": "cdp-deployments"
    }
]

```

`GET /services/cdp-deployments`

```json
{
    "serviceName": "cdp-deployments",
    "githubUrl": "https://github.com/defra-cdp-sandpit/cdp-deployments",
    "imageName": "cdp-deployments"
}
```

### Get Artifacts

`GET /artifacts`

`GET /artifacts/foo/1.2.3`

```json

{
  "created":"2023-05-16T11:03:47.732Z",
  "repo":"cdp-deployments",
  "tag":"v0.21.0",
  "sha256":"sha256:157e63cedba182003e9831047e3f611c516e8d67a4425dfcb15d7d7295c17872",
  "githubUrl":"https://github.com/defra-cdp-sandpit/cdp-deployments",
  "serviceName":"cdp-deployments",
  "scannerVersion":1,
  "files":[
    {"fileName":"Defra.Cdp.Deployments.deps.json","path":"app/Defra.Cdp.Deployments.deps.json","layerSha256":"sha256:115ae824b21b9f671e304112f034096061d8d56b655509561207084fd1f3ccde"}
  ]
}
```

### Getting content of a file

`GET /files/sha256:115ae824b21b9f671e304112f034096061d8d56b655509561207084fd1f3ccde?path=app%2FDefra.Cdp.Deployments.deps.json`
(you'll likely need to url encode the path parameter)
```json
{
  "name": "cdp-node-frontend-exemplar",
  "version": "0.1.0",
  "lockfileVersion": 3,
  "requires": true,
  "packages": {
   ...
```
Takes the layerSha256 value from the artifact data and the fileName as a query param.
Returns a 404 if the filename is invalid.
If you can retrieve the artifact, you have all the data required to access the file (digest and path).

### Getting lists of deployable artifacts

`GET /deployables`

`GET /deployables/image-name`

Both provide a list of docker image names and tags for that image.

### Admin backend
`POST /admin/scan?repo=foo&tag=1.2.3`
Triggers a scan of the given image/tag.

`POST /admin/backfill`
Rescan everything in the docker registry. This will take a long time!
Currently it will *not* drop the existing records, that should be done manually.

### Getting generic lists of deployments

`GET /deployments` returns the most recent deployment events, 1 per task. For services with more than one instance you'll get 1 event per instance rather than the whole deployment

`GET /deployments/:deploymentId` returns details about a specific deployment

### Getting lists of deployments for each environment

`GET /whats-running-where` return the most recent deployment for each service in each environment

`GET /whats-running-where/:service` return the most recent deployment for a given service in each environment


## Local Development
- Install [awslocal](https://github.com/localstack/awscli-local)
- Run localstack Docker container:
```bash
docker run -d -p 4566:4566 -p 4510-4559:4510-4559 localstack/localstack:latest
```
- Create local queue:
```bash
awslocal sqs create-queue --queue-name ecs-deployments
```
- Run [cdp-deployables](https://github.com/defra-cdp-sandpit/cdp-deployables) and populate it with some artifacts
- Generate fake deployments:
```bash
./generate-fake-deployments.sh service-name version
```
The service-name and version will need to exist in cdp-deployables.

## Simulating ECR messages locally
This assume localstack is running and awslocal is installed. If you're not using awslocal, just replace the command with the normal aws command line + localstack connection details.

Create the queue:
```bash
 $ awslocal sqs create-queue --queue-name ecr-push-events
```

Send an event:
```bash
 $ awslocal sqs send-message --queue-url "http://127.0.0.1:4566/000000000000/ecr-push-events" --message-body '{"detail": { "result": "SUCCESS", "action-type": "PUSH", "image-tag": "1.0.0", "repository-name": "cdp-node-frontend-exemplar"}}'
```

### Install MongoDB
- Install [MongoDB](https://www.mongodb.com/docs/manual/tutorial/#installation) on your local machine
  - Consider docker if running on Linux or WSL2 for Windows 10/11
  - Consider Homebrew for MacOS as Mongodb in docker for Apple Silicon can be temperamental
- Start MongoDB:

Here is a way if you want to run it natively locally
```bash
sudo mongod --dbpath ~/mongodb-cdp
```

E.g. running it in docker 
```bash
docker run --name mongodb-cdp -d -p 27017:27017 mongodb/mongodb-community-server:6.0.7-ubuntu2204
```

Note: using the ubuntu image to avoid licensing issues with the ubi8 (redhat linux) image

E.g. running it with Homebrew on Mac if installing mongodb community edition
```bash
brew services start mongodb-community@6.0
```

### Inspect MongoDB

To inspect the Database and Collections locally:
```bash
mongosh
```


### Running

Run `cdp-portal-backend` application:
```bash
dotnet run --project Defra.Cdp.Backend.Api --launch-profile Development
```

