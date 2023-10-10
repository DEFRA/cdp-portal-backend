#!/bin/bash

# check whether user had supplied -h or --help . If yes display usage
if [[ ( $@ == "--help") ||  $@ == "-h" ]]
then 
        echo "Sends an ECR style event to a localstack SQS queue to be consumed by cdp-deployments service."
	echo "Usage: $0 image version aws-region (opt)"
	exit 0
fi 

# TODO: replace these account id's with ones that map to actual environments
accounts=("111111111" "222222222" "333333333" "444444444" "555555555" "666666666" "777777777")

TS=$(date -u +"%Y-%m-%dT%H:%M:%S.000Z")
IMAGE="${1}"
TAG="${2}"
REGION=us-east-1
UUID=$(uuidgen)
for acc in "${accounts[@]}"
do

	payload="{\"version\": \"0\", \"id\": \"$UUID\", \"detail-type\": \"ECS Task State Change\",  \"source\": \"aws.ecs\",  \"account\": \"${acc}\", \"region\": \"$REGION\", \"resources\": [ \"arn:aws:ecs:us-west-2:${acc}:task/FargateCluster/c13b4cb40f1f4fe4a2971f76ae5a47ad\" ], \"detail\": { \"availabilityZone\": \"eu-west-2c\", \"containers\": [ { \"lastStatus\": \"RUNNING\", \"name\": \"FargateApp\", \"image\": \"${acc}.dkr.ecr.us-west-2.amazonaws.com/$IMAGE:$TAG\", \"imageDigest\": \"sha256:74b2c688c700ec95a93e478cdb959737c148df3fbf5ea706abe0318726e885e6\" } ], \"taskDefinitionArn\": \"$(uuidgen)\", \"createdAt\": \"$TS\", \"cpu\": \"256\", \"memory\": \"512\", \"desiredStatus\": \"RUNNING\", \"lastStatus\": \"RUNNING\", \"connectivity\": \"CONNECTED\", \"connectivityAt\": \"$TS\", \"pullStartedAt\": \"$TS\", \"startedAt\": \"$TS\", \"pullStoppedAt\": \"$TS\", \"updatedAt\": \"$TS\" } }"
	echo "Sending deployment event for ${acc}"
	awslocal --region="$REGION" --no-verify-ssl sqs send-message --queue-url https://localhost:4566/000000000000/ecs-deployments --message-body "$payload"
done
