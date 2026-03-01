Параметры бд postgres:
>> docker run -d `
>>   --name my_postgres `
>>   -e POSTGRES_PASSWORD=mysecretpassword `
>>   -v pgdata:/var/lib/postgresql `
>>   -p 5532:5432 `
>>   postgres:latest
